"""Finite negative contract tests; synthetic fixtures below are not gameplay evidence."""
import copy,unittest
from cliff_report import validate
from cliff_motion import inside,point_segment,segment_distance

def fixture():
    trials=[]
    for seed in [42,21193,7727,314159]:
        for region in ['straight','corner']:
            for shape in ['LOWER_W_HOLD','UPPER_S_EDGE','ALONG_D','ALONG_A','DIAGONAL_WD','DIAGONAL_WA']:
                trials.append(dict(seed=seed,region=region,shape=shape,status='PASS',footIntruded=False,centerCovered=True,steps=48))
    placements=[dict(seed=seed,category=kind,expectedAllowed=legal,allowed=legal,active=legal,reason='OK' if legal else 'ROCK') for seed in [42,21193,7727,314159] for kind in ['small-no-Rigidbody','large-static-box','trigger-drop-probe','pool-reactivation-probe'] for legal in [True,False]]
    surfaces=dict(identity='TEST_FIXTURE_ONLY',surfaces=[dict(seed=seed,surfaceLegal=100,surfaceReachable=100,projectedFaceCoreChecks=20,southFaces=20) for seed in range(24)])
    return dict(identity='TEST_FIXTURE_ONLY',status='PASS',trials=trials,placements=placements),surfaces

class EvidenceContracts(unittest.TestCase):
    def test_fixture_shape(self):
        a,b=fixture();self.assertEqual([],validate(a,b))
    def test_intrusion_cannot_be_pass(self):
        a,b=fixture();a['trials'][0]['footIntruded']=True;self.assertTrue(validate(a,b))
    def test_no_actual_physics_coverage_cannot_be_pass(self):
        a,b=fixture();a['trials'][0]['centerCovered']=False;self.assertTrue(validate(a,b))
    def test_missing_seed_cannot_be_hidden(self):
        a,b=fixture();a['trials']=[r for r in a['trials'] if r['seed']!=42];self.assertTrue(validate(a,b))
    def test_missing_whole_direction(self):
        a,b=fixture();a['trials']=[r for r in a['trials'] if r['shape']!='UPPER_S_EDGE'];self.assertTrue(validate(a,b))
    def test_trigger_activation_on_rock_is_rejected(self):
        a,b=fixture();r=next(r for r in a['placements'] if r['category']=='trigger-drop-probe' and not r['expectedAllowed']);r['active']=True;self.assertTrue(validate(a,b))
    def test_missing_pool_pair(self):
        a,b=fixture();a['placements']=a['placements'][:-1];self.assertTrue(validate(a,b))
    def test_unreachable_surface(self):
        a,b=fixture();b['surfaces'][0]['surfaceReachable']=99;self.assertTrue(validate(a,b))
    def test_build_mismatch(self):
        a,b=fixture();b['identity']='OTHER';self.assertTrue(validate(a,b))
    def test_sweep_both_safe_endpoints_crossing_wall(self):
        self.assertEqual(0,segment_distance((-1,.5),(2,.5),(0,0),(0,1)))
    def test_center_outside_but_foot_reaches_wall(self):
        self.assertLess(point_segment((-.1,.5),(0,0),(0,1)),.3)
    def test_corner_outside(self):
        self.assertFalse(inside((-.2,-.2),[(0,0),(1,0),(1,1),(0,1)]))
        self.assertAlmostEqual(2**.5*.2,point_segment((-.2,-.2),(0,0),(1,0)))

if __name__=='__main__':unittest.main(verbosity=2)

