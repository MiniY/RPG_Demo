// Window-only WGC bridge. No monitor API, desktop fallback, input, or target termination.
#include <windows.h>
#include <d3d11.h>
#include <dxgi1_2.h>
#include <windows.graphics.capture.interop.h>
#include <windows.graphics.directx.direct3d11.interop.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Graphics.Capture.h>
#include <winrt/Windows.Graphics.DirectX.h>
#include <winrt/Windows.Graphics.DirectX.Direct3D11.h>
#include <iostream>
#include <vector>
#include <chrono>
#include <thread>
#include <io.h>
#include <fcntl.h>
using namespace winrt;
using namespace winrt::Windows::Graphics;
using namespace winrt::Windows::Graphics::Capture;
using namespace winrt::Windows::Graphics::DirectX;
using namespace winrt::Windows::Graphics::DirectX::Direct3D11;
struct Handle { HANDLE value; ~Handle(){if(value)CloseHandle(value);} };
int wmain(int argc,wchar_t** argv) {
 try {
  init_apartment(apartment_type::multi_threaded);
  if(argc==2 && std::wstring(argv[1])==L"--probe") {
   std::cout << "WGC_SUPPORTED=" << GraphicsCaptureSession::IsSupported() << "\n";return GraphicsCaptureSession::IsSupported()?0:3;
  }
  if(argc!=4)throw std::runtime_error("Expected exact HWND PID creationTime100ns");
  HWND hwnd=reinterpret_cast<HWND>(std::stoull(argv[1]));DWORD pid=std::stoul(argv[2]);auto expected=std::stoull(argv[3]);
  DWORD owner=0;GetWindowThreadProcessId(hwnd,&owner);
  if(!IsWindow(hwnd)||owner!=pid||!IsWindowVisible(hwnd))throw std::runtime_error("OwnedVisibleWindowRequired");
  Handle target{OpenProcess(SYNCHRONIZE|PROCESS_QUERY_LIMITED_INFORMATION,FALSE,pid)};
  if(!target.value)throw std::runtime_error("NativeOwnerOpenFailed");
  FILETIME created{},exited{},kernel{},user{};
  if(!GetProcessTimes(target.value,&created,&exited,&kernel,&user))throw std::runtime_error("NativeOwnerTimeFailed");
  auto actual=(static_cast<unsigned long long>(created.dwHighDateTime)<<32)|created.dwLowDateTime;
  if(actual!=expected||WaitForSingleObject(target.value,0)!=WAIT_TIMEOUT)throw std::runtime_error("NativeOwnerChangedOrExited");
  if(!GraphicsCaptureSession::IsSupported())throw std::runtime_error("WGCUnsupported");
  com_ptr<ID3D11Device> device;com_ptr<ID3D11DeviceContext> context;
  check_hresult(D3D11CreateDevice(nullptr,D3D_DRIVER_TYPE_HARDWARE,nullptr,D3D11_CREATE_DEVICE_BGRA_SUPPORT,nullptr,0,D3D11_SDK_VERSION,device.put(),nullptr,context.put()));
  auto dxgi=device.as<IDXGIDevice>();com_ptr<::IInspectable> inspectable;
  check_hresult(CreateDirect3D11DeviceFromDXGIDevice(dxgi.get(),inspectable.put()));
  auto rtDevice=inspectable.as<IDirect3DDevice>();
  auto factory=get_activation_factory<GraphicsCaptureItem,IGraphicsCaptureItemInterop>();GraphicsCaptureItem item{nullptr};
  check_hresult(factory->CreateForWindow(hwnd,guid_of<GraphicsCaptureItem>(),put_abi(item)));
  auto size=item.Size();if(size.Width<640||size.Height<360||size.Width>4096||size.Height>2400)throw std::runtime_error("WindowSizeOutOfBounds");
  auto pool=Direct3D11CaptureFramePool::CreateFreeThreaded(rtDevice,DirectXPixelFormat::B8G8R8A8UIntNormalized,3,size);
  auto session=pool.CreateCaptureSession(item);session.IsCursorCaptureEnabled(false);session.StartCapture();
  D3D11_TEXTURE2D_DESC desc{};desc.Width=size.Width;desc.Height=size.Height;desc.MipLevels=1;desc.ArraySize=1;desc.Format=DXGI_FORMAT_B8G8R8A8_UNORM;desc.SampleDesc.Count=1;desc.Usage=D3D11_USAGE_STAGING;desc.CPUAccessFlags=D3D11_CPU_ACCESS_READ;
  com_ptr<ID3D11Texture2D> staging;check_hresult(device->CreateTexture2D(&desc,nullptr,staging.put()));
  _setmode(_fileno(stdout),_O_BINARY);std::cout<<"WGC_BGRA "<<size.Width<<" "<<size.Height<<"\n"<<std::flush;
  std::vector<char> pixels(static_cast<size_t>(size.Width)*size.Height*4);bool seen=false;unsigned long long frames=0,updates=0;
  auto start=std::chrono::steady_clock::now();auto next=start;std::string reason;
  while(true) {
   DWORD wait=WaitForSingleObject(target.value,0);if(wait==WAIT_OBJECT_0){reason="EXACT_PLAYER_EXITED";break;}if(wait!=WAIT_TIMEOUT)throw std::runtime_error("OwnerWaitUnknown");
   DWORD nowOwner=0;GetWindowThreadProcessId(hwnd,&nowOwner);if(!IsWindow(hwnd)){reason="OWNED_WINDOW_UNAVAILABLE";break;}if(nowOwner!=pid)throw std::runtime_error("WindowOwnerChanged");
   auto now=std::chrono::steady_clock::now();if(now-start>std::chrono::seconds(1500))throw std::runtime_error("CaptureTimeLimit");
   auto frame=pool.TryGetNextFrame();
   if(frame){
    auto content=frame.ContentSize();if(content.Width!=size.Width||content.Height!=size.Height)throw std::runtime_error("WindowResized");
    com_ptr<ID3D11Texture2D> texture;auto access=frame.Surface().as<::Windows::Graphics::DirectX::Direct3D11::IDirect3DDxgiInterfaceAccess>();check_hresult(access->GetInterface(__uuidof(ID3D11Texture2D),texture.put_void()));
    context->CopyResource(staging.get(),texture.get());D3D11_MAPPED_SUBRESOURCE mapped{};check_hresult(context->Map(staging.get(),0,D3D11_MAP_READ,0,&mapped));
    for(int row=0;row<size.Height;row++)memcpy(pixels.data()+static_cast<size_t>(row)*size.Width*4,static_cast<char*>(mapped.pData)+static_cast<size_t>(row)*mapped.RowPitch,size.Width*4);
    context->Unmap(staging.get(),0);frame.Close();seen=true;++updates;
   }
   if(!seen){if(now-start>std::chrono::seconds(10))throw std::runtime_error("NoWGCFrame");std::this_thread::sleep_for(std::chrono::milliseconds(5));continue;}
   if(std::cout.write(pixels.data(),pixels.size()).fail())throw std::runtime_error("OwnedEncoderPipeClosed");++frames;
   next+=std::chrono::microseconds(83333);std::this_thread::sleep_until(next);
  }
  session.Close();pool.Close();std::cerr<<"WGC_STOP="<<reason<<" frames="<<frames<<" updates="<<updates<<"\n";return frames>=2?0:4;
 } catch(hresult_error const& e){std::cerr<<"WGC_ERROR="<<to_string(e.message())<<" HRESULT="<<std::hex<<e.code().value<<"\n";return 5;}
 catch(std::exception const& e){std::cerr<<"WGC_ERROR="<<e.what()<<"\n";return 6;}
}
