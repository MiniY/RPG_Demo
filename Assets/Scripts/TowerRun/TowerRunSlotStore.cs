using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public enum TowerRunFileProbeState
{
    Missing,
    Exists,
    Unavailable
}

public interface ITowerRunFileSystem
{
    TowerRunFileProbeState ProbeFile(string path);
    string ReadAllText(string path);
    void WriteAllText(string path, string contents);
    void CreateDirectory(string path);
    void ReplaceFile(string source, string destination, string backup);
    void MoveFile(string source, string destination);
    void DeleteFile(string path);
}

public interface ITowerRunNativeFileReplacer
{
    void Replace(string source, string destination, string backup);
}

public sealed class TowerRunNativeFileReplacer : ITowerRunNativeFileReplacer
{
    public void Replace(string source, string destination, string backup) =>
        File.Replace(source, destination, backup, true);
}

public sealed class TowerRunPhysicalFileSystem : ITowerRunFileSystem
{
    private readonly ITowerRunNativeFileReplacer nativeFileReplacer;

    public TowerRunPhysicalFileSystem()
        : this(new TowerRunNativeFileReplacer())
    {
    }

    public TowerRunPhysicalFileSystem(ITowerRunNativeFileReplacer nativeFileReplacer)
    {
        this.nativeFileReplacer = nativeFileReplacer ??
            throw new ArgumentNullException(nameof(nativeFileReplacer));
    }

    public TowerRunFileProbeState ProbeFile(string path)
    {
        try
        {
            File.GetAttributes(path);
            return TowerRunFileProbeState.Exists;
        }
        catch (FileNotFoundException)
        {
            return TowerRunFileProbeState.Missing;
        }
        catch (DirectoryNotFoundException)
        {
            return TowerRunFileProbeState.Missing;
        }
        catch (Exception exception) when (
            exception is UnauthorizedAccessException ||
            exception is IOException ||
            exception is System.Security.SecurityException)
        {
            return TowerRunFileProbeState.Unavailable;
        }
    }

    public string ReadAllText(string path) => File.ReadAllText(path, Encoding.UTF8);
    public void WriteAllText(string path, string contents) =>
        File.WriteAllText(path, contents, new UTF8Encoding(false));
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);
    public void ReplaceFile(string source, string destination, string backup) =>
        nativeFileReplacer.Replace(source, destination, backup);

    public void MoveFile(string source, string destination) => File.Move(source, destination);
    public void DeleteFile(string path) => File.Delete(path);
}

public enum TowerRunSlotState
{
    Empty,
    Valid,
    Corrupt,
    Incompatible,
    Unavailable
}

public sealed class TowerRunSlotSummary
{
    public int slotNumber;
    public TowerRunSlotState state;
    public string playerName;
    public string floorDisplayName;
    public int floorNumber;
    public string savedAtUtc;
    public float currentHealth;
    public int maxHealth;
}

public sealed class TowerRunSlotReadResult
{
    public TowerRunSlotState state;
    public TowerRunCheckpointRecord record;
    public TowerRunSlotSummary summary;
    public string message;
    public string confirmationToken;
}

public sealed class TowerRunSlotWriteResult
{
    public bool succeeded;
    public bool requiresOverwriteConfirmation;
    public bool requiresVerification;
    public string message;
}

public sealed class TowerRunCompatibilityPolicy
{
    private const int MinimumPlayerMaximum = 10;
    private const int MaximumPlayerMaximum = 999;

    private static readonly string[] RequiredRootProperties =
        { "schemaVersion", "runId", "playerName", "savedAtUtc", "floor", "generation", "player" };
    private static readonly string[] RequiredFloorProperties = { "floorNumber", "displayName" };
    private static readonly string[] RequiredGenerationProperties =
    {
        "generatorVersion", "configurationIdentity", "profileId", "requestedSeed",
        "attemptIndex", "staticBaselineSignature"
    };
    private static readonly string[] RequiredPlayerProperties = { "stats", "inventory", "shops" };
    private static readonly string[] RequiredStatsProperties =
    {
        "maxHealth", "currentHealth", "maxMana", "currentMana", "maxStamina", "currentStamina"
    };
    private static readonly string[] RequiredInventoryEntryProperties = { "rewardId", "amount" };
    private static readonly string[] RequiredShopProperties = { "catalogId", "productStocks" };
    private static readonly string[] RequiredStockProperties = { "productId", "quantity" };

    public TowerRunCompatibilityPolicy(
        int schemaVersion,
        int generatorVersion,
        string configurationIdentity)
    {
        SchemaVersion = schemaVersion;
        GeneratorVersion = generatorVersion;
        ConfigurationIdentity = configurationIdentity ?? string.Empty;
    }

    public int SchemaVersion { get; }
    public int GeneratorVersion { get; }
    public string ConfigurationIdentity { get; }

    public TowerRunSlotState Evaluate(TowerRunCheckpointRecord record, out string message)
    {
        if (record == null)
        {
            message = "存档结构不完整。";
            return TowerRunSlotState.Corrupt;
        }

        if (record.schemaVersion != SchemaVersion)
        {
            message = "存档格式版本不支持。";
            return TowerRunSlotState.Incompatible;
        }

        if (string.IsNullOrWhiteSpace(record.runId) ||
            record.floor == null ||
            record.floor.floorNumber < 0 ||
            string.IsNullOrWhiteSpace(record.floor.displayName) ||
            record.player == null ||
            record.player.stats == null ||
            record.generation == null ||
            record.generation.attemptIndex < 0 ||
            string.IsNullOrWhiteSpace(record.generation.configurationIdentity) ||
            string.IsNullOrWhiteSpace(record.generation.profileId) ||
            string.IsNullOrWhiteSpace(record.generation.staticBaselineSignature))
        {
            message = "存档结构不完整。";
            return TowerRunSlotState.Corrupt;
        }

        if (record.generation.generatorVersion != GeneratorVersion ||
            !string.Equals(
                record.generation.configurationIdentity,
                ConfigurationIdentity,
                StringComparison.Ordinal))
        {
            message = "地图生成版本或配置身份不兼容。";
            return TowerRunSlotState.Incompatible;
        }

        if (!IsPlayerDataValid(record.player))
        {
            message = "存档中的玩家、背包或商店数据无效。";
            return TowerRunSlotState.Corrupt;
        }

        message = string.Empty;
        return TowerRunSlotState.Valid;
    }

    public TowerRunSlotState EvaluatePersisted(
        TowerRunCheckpointRecord record,
        string rawJson,
        out string message)
    {
        TowerRunSlotState state = Evaluate(record, out message);

        if (state != TowerRunSlotState.Valid)
            return state;

        if (!HasRequiredPersistedProperties(rawJson) ||
            string.IsNullOrWhiteSpace(record.playerName) ||
            string.IsNullOrWhiteSpace(record.savedAtUtc) ||
            !DateTime.TryParseExact(
                record.savedAtUtc,
                "O",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out _))
        {
            message = "存档缺少完整的恢复输入或有效保存时间。";
            return TowerRunSlotState.Corrupt;
        }

        return TowerRunSlotState.Valid;
    }

    private static bool IsPlayerDataValid(TowerRunPlayerRecord player)
    {
        TowerRunPlayerStatsRecord stats = player.stats;

        if (stats.maxHealth < MinimumPlayerMaximum ||
            stats.maxHealth > MaximumPlayerMaximum ||
            stats.maxMana < MinimumPlayerMaximum ||
            stats.maxMana > MaximumPlayerMaximum ||
            stats.maxStamina < MinimumPlayerMaximum ||
            stats.maxStamina > MaximumPlayerMaximum ||
            !IsFinite(stats.currentHealth) ||
            !IsFinite(stats.currentMana) ||
            !IsFinite(stats.currentStamina) ||
            stats.currentHealth < 0f || stats.currentHealth > stats.maxHealth ||
            stats.currentMana < 0f || stats.currentMana > stats.maxMana ||
            stats.currentStamina < 0f || stats.currentStamina > stats.maxStamina ||
            player.inventory == null || player.shops == null)
        {
            return false;
        }

        HashSet<string> rewardIds = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < player.inventory.Count; i++)
        {
            TowerRunInventoryEntry entry = player.inventory[i];

            if (entry == null || string.IsNullOrWhiteSpace(entry.rewardId) ||
                entry.amount <= 0L || entry.amount > int.MaxValue ||
                !rewardIds.Add(entry.rewardId))
            {
                return false;
            }
        }

        HashSet<string> catalogIds = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < player.shops.Count; i++)
        {
            TowerRunShopRecord shop = player.shops[i];

            if (shop == null || string.IsNullOrWhiteSpace(shop.catalogId) ||
                !catalogIds.Add(shop.catalogId) || shop.productStocks == null)
            {
                return false;
            }

            HashSet<string> productIds = new HashSet<string>(StringComparer.Ordinal);

            for (int j = 0; j < shop.productStocks.Count; j++)
            {
                TowerRunShopStockRecord stock = shop.productStocks[j];

                if (stock == null || string.IsNullOrWhiteSpace(stock.productId) ||
                    stock.quantity < 0L || stock.quantity > int.MaxValue ||
                    !productIds.Add(stock.productId))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static bool HasRequiredPersistedProperties(string json)
    {
        if (!PersistedJsonParser.TryParse(json, out PersistedJsonNode root) ||
            !HasObjectProperties(root, RequiredRootProperties) ||
            !TryGetObject(root, "floor", out PersistedJsonNode floor) ||
            !HasObjectProperties(floor, RequiredFloorProperties) ||
            !TryGetObject(root, "generation", out PersistedJsonNode generation) ||
            !HasObjectProperties(generation, RequiredGenerationProperties) ||
            !TryGetObject(root, "player", out PersistedJsonNode player) ||
            !HasObjectProperties(player, RequiredPlayerProperties) ||
            !TryGetObject(player, "stats", out PersistedJsonNode stats) ||
            !HasObjectProperties(stats, RequiredStatsProperties) ||
            !TryGetArray(player, "inventory", out PersistedJsonNode inventory) ||
            !TryGetArray(player, "shops", out PersistedJsonNode shops))
        {
            return false;
        }

        for (int i = 0; i < inventory.Items.Count; i++)
        {
            if (!HasObjectProperties(inventory.Items[i], RequiredInventoryEntryProperties))
                return false;
        }

        for (int i = 0; i < shops.Items.Count; i++)
        {
            PersistedJsonNode shop = shops.Items[i];

            if (!HasObjectProperties(shop, RequiredShopProperties) ||
                !TryGetArray(shop, "productStocks", out PersistedJsonNode productStocks))
            {
                return false;
            }

            for (int j = 0; j < productStocks.Items.Count; j++)
            {
                if (!HasObjectProperties(productStocks.Items[j], RequiredStockProperties))
                    return false;
            }
        }

        return true;
    }

    private static bool HasObjectProperties(PersistedJsonNode node, string[] propertyNames)
    {
        if (node == null || node.Kind != PersistedJsonKind.Object)
            return false;

        for (int i = 0; i < propertyNames.Length; i++)
        {
            if (!node.Properties.ContainsKey(propertyNames[i]))
                return false;
        }

        return true;
    }

    private static bool TryGetObject(
        PersistedJsonNode owner,
        string propertyName,
        out PersistedJsonNode value)
    {
        return owner.Properties.TryGetValue(propertyName, out value) &&
               value.Kind == PersistedJsonKind.Object;
    }

    private static bool TryGetArray(
        PersistedJsonNode owner,
        string propertyName,
        out PersistedJsonNode value)
    {
        return owner.Properties.TryGetValue(propertyName, out value) &&
               value.Kind == PersistedJsonKind.Array;
    }

    private enum PersistedJsonKind
    {
        Scalar,
        Object,
        Array
    }

    private sealed class PersistedJsonNode
    {
        public PersistedJsonKind Kind;
        public readonly Dictionary<string, PersistedJsonNode> Properties =
            new Dictionary<string, PersistedJsonNode>(StringComparer.Ordinal);
        public readonly List<PersistedJsonNode> Items = new List<PersistedJsonNode>();
    }

    private sealed class PersistedJsonParser
    {
        private readonly string json;
        private int index;

        private PersistedJsonParser(string json)
        {
            this.json = json;
        }

        public static bool TryParse(string json, out PersistedJsonNode root)
        {
            root = null;

            if (string.IsNullOrWhiteSpace(json))
                return false;

            PersistedJsonParser parser = new PersistedJsonParser(json);

            if (!parser.TryParseValue(out root))
                return false;

            parser.SkipWhitespace();
            return parser.index == json.Length && root.Kind == PersistedJsonKind.Object;
        }

        private bool TryParseValue(out PersistedJsonNode node)
        {
            SkipWhitespace();
            node = null;

            if (index >= json.Length)
                return false;

            if (json[index] == '{')
                return TryParseObject(out node);

            if (json[index] == '[')
                return TryParseArray(out node);

            if (json[index] == '"')
            {
                if (!TryParseString(out _))
                    return false;

                node = new PersistedJsonNode { Kind = PersistedJsonKind.Scalar };
                return true;
            }

            int start = index;

            while (index < json.Length && json[index] != ',' &&
                   json[index] != '}' && json[index] != ']')
            {
                index++;
            }

            if (string.IsNullOrWhiteSpace(json.Substring(start, index - start)))
                return false;

            node = new PersistedJsonNode { Kind = PersistedJsonKind.Scalar };
            return true;
        }

        private bool TryParseObject(out PersistedJsonNode node)
        {
            node = new PersistedJsonNode { Kind = PersistedJsonKind.Object };
            index++;
            SkipWhitespace();

            if (TryConsume('}'))
                return true;

            while (index < json.Length)
            {
                if (!TryParseString(out string propertyName))
                    return false;

                SkipWhitespace();

                if (!TryConsume(':') || !TryParseValue(out PersistedJsonNode value) ||
                    node.Properties.ContainsKey(propertyName))
                {
                    return false;
                }

                node.Properties.Add(propertyName, value);
                SkipWhitespace();

                if (TryConsume('}'))
                    return true;

                if (!TryConsume(','))
                    return false;

                SkipWhitespace();
            }

            return false;
        }

        private bool TryParseArray(out PersistedJsonNode node)
        {
            node = new PersistedJsonNode { Kind = PersistedJsonKind.Array };
            index++;
            SkipWhitespace();

            if (TryConsume(']'))
                return true;

            while (index < json.Length)
            {
                if (!TryParseValue(out PersistedJsonNode item))
                    return false;

                node.Items.Add(item);
                SkipWhitespace();

                if (TryConsume(']'))
                    return true;

                if (!TryConsume(','))
                    return false;

                SkipWhitespace();
            }

            return false;
        }

        private bool TryParseString(out string value)
        {
            value = null;

            if (!TryConsume('"'))
                return false;

            StringBuilder builder = new StringBuilder();

            while (index < json.Length)
            {
                char current = json[index++];

                if (current == '"')
                {
                    value = builder.ToString();
                    return true;
                }

                if (current != '\\')
                {
                    builder.Append(current);
                    continue;
                }

                if (index >= json.Length)
                    return false;

                char escaped = json[index++];

                if (escaped == 'u')
                {
                    if (index + 4 > json.Length ||
                        !ushort.TryParse(
                            json.Substring(index, 4),
                            System.Globalization.NumberStyles.HexNumber,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out ushort codePoint))
                    {
                        return false;
                    }

                    builder.Append((char)codePoint);
                    index += 4;
                }
                else
                {
                    builder.Append(escaped);
                }
            }

            return false;
        }

        private bool TryConsume(char expected)
        {
            if (index >= json.Length || json[index] != expected)
                return false;

            index++;
            return true;
        }

        private void SkipWhitespace()
        {
            while (index < json.Length && char.IsWhiteSpace(json[index]))
                index++;
        }
    }
}

public sealed class TowerRunSlotStore
{
    public const int FirstSlotNumber = 1;
    public const int LastSlotNumber = 3;

    private readonly string rootDirectory;
    private readonly TowerRunCompatibilityPolicy compatibility;
    private readonly ITowerRunFileSystem fileSystem;

    public TowerRunSlotStore(string rootDirectory, TowerRunCompatibilityPolicy compatibility)
        : this(rootDirectory, compatibility, new TowerRunPhysicalFileSystem())
    {
    }

    public TowerRunSlotStore(
        string rootDirectory,
        TowerRunCompatibilityPolicy compatibility,
        ITowerRunFileSystem fileSystem)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
            throw new ArgumentException("A slot root directory is required.", nameof(rootDirectory));

        this.rootDirectory = rootDirectory;
        this.compatibility = compatibility ?? throw new ArgumentNullException(nameof(compatibility));
        this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    public TowerRunSlotReadResult ReadSlot(int slotNumber)
    {
        ValidateSlotNumber(slotNumber);
        string path = GetSlotPath(slotNumber);
        string confirmationToken = null;

        try
        {
            TowerRunFileProbeState probe = fileSystem.ProbeFile(path);

            if (probe == TowerRunFileProbeState.Missing)
            {
                TowerRunFileProbeState backupProbe = fileSystem.ProbeFile(path + ".backup");

                if (backupProbe != TowerRunFileProbeState.Missing)
                {
                    return CreateReadResult(
                        slotNumber,
                        TowerRunSlotState.Unavailable,
                        null,
                        "检测到未完成或无法核验的槽位替换，请保留现有文件并稍后重试。",
                        null);
                }

                return CreateReadResult(slotNumber, TowerRunSlotState.Empty, null, "空槽位。", "empty");
            }

            if (probe == TowerRunFileProbeState.Unavailable)
            {
                return CreateReadResult(
                    slotNumber,
                    TowerRunSlotState.Unavailable,
                    null,
                    "当前无法可靠访问该槽位。",
                    null);
            }

            string json = fileSystem.ReadAllText(path);
            confirmationToken = ComputeConfirmationToken(json);
            TowerRunCheckpointRecord parsed = JsonUtility.FromJson<TowerRunCheckpointRecord>(json);
            TowerRunSlotState state = compatibility.EvaluatePersisted(parsed, json, out string message);

            return CreateReadResult(
                slotNumber,
                state,
                state == TowerRunSlotState.Valid ? parsed.DeepClone() : null,
                message,
                confirmationToken);
        }
        catch (UnauthorizedAccessException exception)
        {
            return CreateReadResult(slotNumber, TowerRunSlotState.Unavailable, null, exception.Message, null);
        }
        catch (IOException exception)
        {
            return CreateReadResult(slotNumber, TowerRunSlotState.Unavailable, null, exception.Message, null);
        }
        catch (Exception exception)
        {
            return CreateReadResult(
                slotNumber,
                TowerRunSlotState.Corrupt,
                null,
                exception.Message,
                confirmationToken);
        }
    }

    public TowerRunSlotWriteResult TryCommit(
        int slotNumber,
        TowerRunCheckpointRecord record,
        bool overwriteConfirmed,
        string expectedConfirmationToken = null)
    {
        ValidateSlotNumber(slotNumber);

        TowerRunSlotState candidateState = compatibility.Evaluate(record, out string candidateMessage);

        if (candidateState != TowerRunSlotState.Valid)
            return WriteFailure(candidateMessage);

        TowerRunSlotReadResult existing = ReadSlot(slotNumber);

        if (existing.state == TowerRunSlotState.Unavailable)
            return WriteFailure("当前无法可靠访问该槽位，请稍后重试。原记录未被替换。");

        if (existing.state != TowerRunSlotState.Empty && !overwriteConfirmed)
        {
            return new TowerRunSlotWriteResult
            {
                succeeded = false,
                requiresOverwriteConfirmation = true,
                message = "该槽位已有记录，需要明确确认后才能替换。"
            };
        }

        if (existing.state != TowerRunSlotState.Empty &&
            !string.Equals(existing.confirmationToken, expectedConfirmationToken, StringComparison.Ordinal))
        {
            return new TowerRunSlotWriteResult
            {
                succeeded = false,
                requiresOverwriteConfirmation = true,
                message = "槽位内容已变化，请重新查看后确认覆盖。"
            };
        }

        string slotPath = GetSlotPath(slotNumber);
        string temporaryPath = slotPath + ".tmp";
        string backupPath = slotPath + ".backup";
        bool hadExistingFile = false;
        bool formalWriteAttempted = false;
        bool replacementCompleted = false;
        string expectedCommittedToken = null;

        try
        {
            TowerRunFileProbeState formalProbe = fileSystem.ProbeFile(slotPath);

            if (formalProbe == TowerRunFileProbeState.Unavailable)
                return WriteFailure("当前无法可靠访问该槽位，请稍后重试。原记录未被替换。");

            hadExistingFile = formalProbe == TowerRunFileProbeState.Exists;
            fileSystem.CreateDirectory(rootDirectory);
            TowerRunCheckpointRecord committed = record.DeepClone();
            committed.savedAtUtc = DateTime.UtcNow.ToString("O");
            string json = JsonUtility.ToJson(committed, true);
            expectedCommittedToken = ComputeConfirmationToken(json);
            fileSystem.WriteAllText(temporaryPath, json);

            string temporaryJson = fileSystem.ReadAllText(temporaryPath);
            TowerRunCheckpointRecord verification =
                JsonUtility.FromJson<TowerRunCheckpointRecord>(temporaryJson);
            TowerRunSlotState verificationState = compatibility.EvaluatePersisted(
                verification,
                temporaryJson,
                out string verificationMessage);

            if (verificationState != TowerRunSlotState.Valid)
                return WriteFailure(verificationMessage);

            if (hadExistingFile)
            {
                formalWriteAttempted = true;
                fileSystem.ReplaceFile(temporaryPath, slotPath, backupPath);
            }
            else
            {
                formalWriteAttempted = true;
                fileSystem.MoveFile(temporaryPath, slotPath);
            }

            replacementCompleted = true;

            TowerRunSlotReadResult committedRead = ReadSlot(slotNumber);

            if (committedRead.state != TowerRunSlotState.Valid ||
                committedRead.record == null ||
                !string.Equals(
                    committedRead.confirmationToken,
                    expectedCommittedToken,
                    StringComparison.Ordinal))
            {
                bool restored = RestorePreviousFormalRecord(
                    slotPath,
                    backupPath,
                    temporaryPath,
                    hadExistingFile);
                return restored
                    ? WriteFailure("正式槽位写入后的校验失败，原记录已恢复。")
                    : WriteUncertainFailure("正式槽位写入后的校验失败，且无法确认原记录已恢复。请重新核验该槽位。");
            }

            return new TowerRunSlotWriteResult
            {
                succeeded = true,
                message = "槽位保存成功。"
            };
        }
        catch (Exception exception)
        {
            if (replacementCompleted &&
                !RestorePreviousFormalRecord(slotPath, backupPath, temporaryPath, hadExistingFile))
            {
                return WriteUncertainFailure(
                    $"槽位写入失败，且无法确认原记录已恢复。请重新核验该槽位。{Environment.NewLine}{exception.Message}");
            }

            if (formalWriteAttempted && !replacementCompleted)
            {
                TowerRunSlotReadResult observed = ReadSlot(slotNumber);

                if (string.Equals(
                        observed.confirmationToken,
                        expectedCommittedToken,
                        StringComparison.Ordinal) &&
                    observed.state == TowerRunSlotState.Valid)
                {
                    return new TowerRunSlotWriteResult
                    {
                        succeeded = true,
                        message = "槽位保存已通过重新读取确认成功。"
                    };
                }

                if (string.Equals(
                    observed.confirmationToken,
                    existing.confirmationToken,
                    StringComparison.Ordinal))
                {
                    return WriteFailure(exception.Message);
                }

                return WriteUncertainFailure(
                    $"槽位替换结果不确定，请重新核验该槽位。{Environment.NewLine}{exception.Message}");
            }

            return WriteFailure(exception.Message);
        }
        finally
        {
            try
            {
                if (fileSystem.ProbeFile(temporaryPath) == TowerRunFileProbeState.Exists)
                    fileSystem.DeleteFile(temporaryPath);
            }
            catch (Exception)
            {
                // A failed cleanup must not replace the actual write result.
            }
        }
    }

    private bool RestorePreviousFormalRecord(
        string slotPath,
        string backupPath,
        string discardedPath,
        bool hadExistingFile)
    {
        try
        {
            TowerRunFileProbeState backupProbe = fileSystem.ProbeFile(backupPath);
            TowerRunFileProbeState slotProbe = fileSystem.ProbeFile(slotPath);

            if (hadExistingFile && backupProbe == TowerRunFileProbeState.Exists)
            {
                if (slotProbe == TowerRunFileProbeState.Exists)
                    fileSystem.ReplaceFile(backupPath, slotPath, discardedPath);
                else if (slotProbe == TowerRunFileProbeState.Missing)
                    fileSystem.MoveFile(backupPath, slotPath);
                else
                    return false;
            }
            else if (hadExistingFile)
            {
                return false;
            }
            else if (slotProbe == TowerRunFileProbeState.Exists)
            {
                fileSystem.DeleteFile(slotPath);
            }
            else if (slotProbe == TowerRunFileProbeState.Unavailable)
            {
                return false;
            }

            return true;
        }
        catch (Exception)
        {
            // The caller reports an uncertain outcome. A backup is retained for later inspection.
            return false;
        }
    }

    private TowerRunSlotReadResult CreateReadResult(
        int slotNumber,
        TowerRunSlotState state,
        TowerRunCheckpointRecord record,
        string message,
        string confirmationToken)
    {
        TowerRunSlotSummary summary = new TowerRunSlotSummary
        {
            slotNumber = slotNumber,
            state = state
        };

        if (state == TowerRunSlotState.Valid && record != null)
        {
            summary.playerName = record.playerName;
            summary.floorDisplayName = record.floor.displayName;
            summary.floorNumber = record.floor.floorNumber;
            summary.savedAtUtc = record.savedAtUtc;
            summary.currentHealth = record.player.stats.currentHealth;
            summary.maxHealth = record.player.stats.maxHealth;
        }

        return new TowerRunSlotReadResult
        {
            state = state,
            record = record?.DeepClone(),
            summary = summary,
            message = message,
            confirmationToken = confirmationToken
        };
    }

    private static string ComputeConfirmationToken(string text)
    {
        using (SHA256 algorithm = SHA256.Create())
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text ?? string.Empty);
            return BitConverter.ToString(algorithm.ComputeHash(bytes)).Replace("-", string.Empty);
        }
    }

    private string GetSlotPath(int slotNumber)
    {
        return Path.Combine(rootDirectory, $"tower-run-slot-{slotNumber}.json");
    }

    private static TowerRunSlotWriteResult WriteFailure(string message)
    {
        return new TowerRunSlotWriteResult
        {
            succeeded = false,
            message = string.IsNullOrWhiteSpace(message) ? "槽位保存失败。" : message
        };
    }

    private static TowerRunSlotWriteResult WriteUncertainFailure(string message)
    {
        return new TowerRunSlotWriteResult
        {
            succeeded = false,
            requiresVerification = true,
            message = message
        };
    }

    private static void ValidateSlotNumber(int slotNumber)
    {
        if (slotNumber < FirstSlotNumber || slotNumber > LastSlotNumber)
            throw new ArgumentOutOfRangeException(nameof(slotNumber), "Only slots 1 through 3 are supported.");
    }
}
