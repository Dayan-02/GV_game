"""Author original death variants on the existing sculpt rigs, without replacing meshes.
Outputs editable Blender copies and animation-only FBXs. No Unity source is overwritten.
"""
import bpy, math, json, hashlib, sys
from pathlib import Path
from mathutils import Vector, Quaternion, Matrix

ROOT = Path(__file__).resolve().parent / 'DeathRevision'
# Resolve explicitly from the CombatPolish directory, not the process cwd.
SOURCE = Path(__file__).resolve().parent.parent / 'CustomEnemies/SculptTextureRevision4/Blender'
for folder in ('Blender', 'Exports', 'Previews', 'Evidence'):
    (ROOT/folder).mkdir(parents=True, exist_ok=True)
KINDS = ('Hunter', 'Runner', 'Ambusher', 'Elite')
FPS = 30

def smooth(x):
    x=max(0,min(1,x)); return x*x*(3-2*x)

def lerp_keys(keys, t):
    for i in range(len(keys)-1):
        a,b=keys[i],keys[i+1]
        if t<=b[0]:
            u=smooth((t-a[0])/(b[0]-a[0]))
            return Vector(a[1]).lerp(Vector(b[1]),u)
    return Vector(keys[-1][1])

def rotation(name, axis, degrees):
    b=rig.pose.bones[name]; rest=b.bone.matrix_local.to_quaternion()
    b.rotation_quaternion=rest.inverted()@Quaternion(Vector(axis),math.radians(degrees))@rest

def solve(upper, lower, end, target, pole, end_q=None):
    bpy.context.view_layer.update()
    a,b,c=[rig.pose.bones[n] for n in (upper,lower,end)]
    start=a.head.copy(); delta=Vector(target)-start
    l1,l2=a.bone.length,b.bone.length
    d=max(abs(l1-l2)+.004,min(delta.length,(l1+l2)*.99))
    direction=delta.normalized()
    bend=Vector(pole); bend=(bend-direction*bend.dot(direction)).normalized()
    along=(l1*l1+d*d-l2*l2)/(2*d)
    elbow=start+direction*along+bend*math.sqrt(max(0,l1*l1-along*along))
    q=(b.head-start).rotation_difference(elbow-start)@a.matrix.to_quaternion()
    a.matrix=Matrix.LocRotScale(start,q,Vector((1,1,1)))
    bpy.context.view_layer.update()
    q=(c.head-b.head).rotation_difference(start+direction*d-b.head)@b.matrix.to_quaternion()
    b.matrix=Matrix.LocRotScale(b.head,q,Vector((1,1,1)))
    bpy.context.view_layer.update()
    if end_q is not None: c.matrix=Matrix.LocRotScale(c.head,end_q,Vector((1,1,1)))
    bpy.context.view_layer.update()

def pose(kind, mode, t):
    for b in rig.pose.bones:
        b.rotation_mode='QUATERNION'; b.rotation_quaternion=(1,0,0,0); b.location=(0,0,0); b.scale=(1,1,1)
    side=-1 if kind in ('Hunter','Ambusher') else 1
    width=rig.data.bones['LeftUpperLeg'].head_local.x
    # Initial recoil, knees give way, gravity-driven collapse, brief settle, then hold.
    if mode=='DeathBack':
        h=lerp_keys([(0,(0,0,.87)),(.1,(0,.035,.88)),(.26,(.02,.12,.70)),
                    (.53,(.03,.30,.37)),(.69,(.03,.40,.15)),(.8,(.03,.40,.165)),(.91,(.03,.40,.15))],t)
        angle=lerp_keys([(0,(8,0,0)),(.10,(-10,0,0)),(.26,(-22,0,0)),
                        (.53,(-58,0,5)),(.69,(-91,0,9)),(.84,(-87,0,9)),(1,(-89,0,9))],t)
        final_feet=[(width*1.5,-.41,.11),(-width*1.8,-.34,.11)]
        final_hands=[(.62,.81,.11),(-.59,.68,.11)]
    elif mode=='DeathSide':
        h=lerp_keys([(0,(0,0,.87)),(.13,(side*.015,-.015,.85)),(.3,(side*.09,-.06,.64)),
                    (.56,(side*.22,-.12,.35)),(.73,(side*.28,-.18,.16)),(.84,(side*.28,-.18,.175)),(.94,(side*.28,-.18,.16))],t)
        angle=lerp_keys([(0,(8,0,0)),(.13,(15,side*5,0)),(.3,(28,side*18,0)),
                        (.56,(64,side*36,side*5)),(.73,(89,side*48,side*8)),(.9,(87,side*44,side*8))],t)
        final_feet=[(side*.28+width*1.8,.58,.11),(side*.28-width*1.3,.54,.11)]
        final_hands=[(side*.28+.58,-.66,.12),(side*.28-.56,-.54,.12)]
    else:
        h=lerp_keys([(0,(0,0,.87)),(.12,(0,.015,.86)),(.28,(.02,-.06,.66)),
                    (.5,(.04,-.16,.40)),(.69,(.05,-.24,.13)),(.80,(.05,-.24,.145)),(.92,(.05,-.24,.13))],t)
        angle=lerp_keys([(0,(8,0,0)),(.12,(-4,0,0)),(.28,(23,0,-3)),
                        (.5,(59,0,-5)),(.69,(89,0,-7)),(.82,(86,0,-7)),(.94,(88,0,-7))],t)
        final_feet=[(width*1.8,.57,.11),(-width*1.5,.53,.11)]
        final_hands=[(.63,-.84,.11),(-.56,-.68,.11)]
    hip=rig.pose.bones['Hips']
    hip.location=hip.bone.matrix_local.to_3x3().inverted()@(h-hip.bone.head_local)
    rest=hip.bone.matrix_local.to_quaternion()
    q=Quaternion((0,0,1),math.radians(angle.z))@Quaternion((0,1,0),math.radians(angle.y))@Quaternion((1,0,0),math.radians(angle.x))
    hip.rotation_quaternion=rest.inverted()@q@rest
    rotation('Spine',(1,0,0),5*(1-smooth(t/.62)))
    rotation('Chest',(0,0,1),side*4*math.sin(math.pi*min(t/.8,1)))
    rotation('Head',(0,1,0),side*17*smooth((t-.25)/.45))
    rotation('Head',(1,0,0),(-5 if mode=='DeathBack' else 5)*smooth((t-.2)/.5))
    bpy.context.view_layer.update()
    # Fit the torso first, then solve the limbs to the floor. Raising the whole
    # completed pose would lift the feet and hands on the wide armoured Elite.
    if t > .4:
        evaluated=mesh.evaluated_get(bpy.context.evaluated_depsgraph_get()); geom=evaluated.to_mesh()
        low=min((evaluated.matrix_world@geom.vertices[i].co).z for i in body_vertices)
        evaluated.to_mesh_clear()
        lift=max(0,.012-low)/rig.scale.z
        hip.location+=hip.bone.matrix_local.to_3x3().inverted()@Vector((0,0,lift))
        bpy.context.view_layer.update()
    for i,(name,s) in enumerate((('Left',1),('Right',-1))):
        foot_start=Vector((s*width,0,.11))
        foot_end=Vector(final_feet[i])
        foot_target=foot_start.lerp(foot_end,smooth((t-.21-i*.025)/.48))
        foot_rest=rig.data.bones[name+'Foot'].matrix_local.to_quaternion()
        # Keep the sole approximately level, rather than folding it through the shin.
        fq=Quaternion((0,0,1),s*.15*smooth(t/.7))@foot_rest
        pole=Vector((s*.45,-1,0)).lerp(Vector((s*.6,0,.06)),smooth((t-.25)/.4))
        solve(name+'UpperLeg',name+'LowerLeg',name+'Foot',foot_target,pole,fq)
        hand_start=Vector((s*.26,-.24,1.1))
        brace=Vector((s*.32,-.47,.68)) if mode!='DeathBack' else Vector((s*.39,.15,.9))
        shoulder=rig.pose.bones[name+'UpperArm'].head.copy()
        reach=(rig.data.bones[name+'UpperArm'].length+rig.data.bones[name+'LowerArm'].length)*.96
        dz=.075-shoulder.z
        lateral=math.sqrt(max(.012,reach*reach-dz*dz-.08*.08))
        relaxed=Vector((shoulder.x+s*lateral,shoulder.y+.08,.075))
        hand_target=lerp_keys([(0,hand_start),(.18,hand_start+Vector((s*.025,.02,.07))),
                               (.4,brace),(.67,Vector(final_hands[i])+Vector((0,0,.035))),
                               (.94,relaxed)],t)
        hand_rest=rig.data.bones[name+'Hand'].matrix_local.to_quaternion()
        arm_pole=Vector((s*.6,.1,.25)).lerp(Vector((s,.1,.02)),smooth((t-.58)/.30))
        solve(name+'UpperArm',name+'LowerArm',name+'Hand',hand_target,arm_pole,hand_rest)
        for finger in ('Index','Middle','Ring','Little'):
            for segment in (1,2,3):
                rotation(name+finger+str(segment),(0,1,0),s*(16+segment*5)*(1-.65*smooth(t/.8)))

def curves(action):
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags: yield from bag.fcurves

reports=[]
for kind in KINDS:
    source=SOURCE/(kind+'_Sculpt4.blend')
    bpy.ops.wm.open_mainfile(filepath=str(source))
    bpy.context.preferences.filepaths.save_version=0
    rig=bpy.data.objects[kind+'_Rig']; mesh=bpy.data.objects[kind+'_CustomMesh']
    central={g.index for g in mesh.vertex_groups if g.name in ('Hips','Spine','Chest','UpperChest','Neck','Head')}
    body_vertices=[v.index for v in mesh.data.vertices if v.groups and max(v.groups,key=lambda g:g.weight).group in central]
    rig.location=(0,0,0)
    rig.animation_data_clear(); rig.animation_data_create()
    scene=bpy.context.scene; scene.render.fps=FPS
    duration=2.65 if kind=='Elite' else 2.2 if kind=='Runner' else 2.4
    authored=[]
    for mode in ('DeathForward','DeathBack','DeathSide'):
        action=bpy.data.actions.new(kind+'_'+mode); action.use_fake_user=True
        action['source']='Original keyframes on existing custom skeleton; AI-assisted'
        action['loop']=False
        rig.animation_data.action=action
        count=round(duration*FPS)
        corrections=[]; samples=[]
        for f in range(count+1):
            pose(kind,mode,f/count); bpy.context.view_layer.update()
            evaluated=mesh.evaluated_get(bpy.context.evaluated_depsgraph_get()); geom=evaluated.to_mesh()
            low=min((evaluated.matrix_world@v.co).z for v in geom.vertices); evaluated.to_mesh_clear()
            corrections.append(max(0,.006-low)/rig.scale.z)
            samples.append([(b.name,b.rotation_quaternion.copy(),b.location.copy()) for b in rig.pose.bones])
        # Smooth the baked contact correction; there is no frame-by-frame runtime floor snapping.
        for _ in range(3):
            corrections=[sum(corrections[max(0,i-2):min(len(corrections),i+3)])/len(corrections[max(0,i-2):min(len(corrections),i+3)]) for i in range(len(corrections))]
        previous={}
        for f,sample in enumerate(samples):
            for name,q,loc in sample:
                b=rig.pose.bones[name]
                if name in previous and previous[name].dot(q)<0:q=-q
                previous[name]=q.copy(); b.rotation_quaternion=q; b.location=loc
                if name=='Hips':
                    b.location+=b.bone.matrix_local.to_3x3().inverted()@Vector((0,0,corrections[f]))
                    b.keyframe_insert('location',frame=f+1,group=name)
                b.keyframe_insert('rotation_quaternion',frame=f+1,group=name)
        for c in curves(action):
            for key in c.keyframe_points:
                key.interpolation='BEZIER';key.handle_left_type='AUTO_CLAMPED';key.handle_right_type='AUTO_CLAMPED'
        authored.append(action)
        print('AUTHORED',action.name,'floor_adjust_max',round(max(corrections),3),flush=True)
    # Keep all previous actions in the editable copy, but export only the new variants.
    rig.animation_data.action=authored[0]; rig.animation_data.action_slot=authored[0].slots[0]
    scene.frame_start=1;scene.frame_end=count+1;scene.frame_set(1)
    blend=ROOT/'Blender'/(kind+'_DeathRevision.blend')
    bpy.ops.wm.save_as_mainfile(filepath=str(blend))
    for a in list(bpy.data.actions):
        if a not in authored:bpy.data.actions.remove(a)
    bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.context.view_layer.objects.active=rig
    export=ROOT/'Exports'/(kind+'_Deaths.fbx')
    bpy.ops.export_scene.fbx(filepath=str(export),use_selection=True,object_types={'ARMATURE'},
        add_leaf_bones=False,apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',
        axis_forward='-Z',axis_up='Y',bake_anim=True,bake_anim_use_all_actions=True,
        bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,path_mode='STRIP')
    reports.append(dict(kind=kind,source=str(source),source_sha256=hashlib.sha256(source.read_bytes()).hexdigest(),
                        blend=str(blend),export=str(export),actions=[a.name for a in authored],duration=duration))
    if '--render' in sys.argv:
        scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=8
        scene.cycles.use_denoising=True;scene.render.use_simplify=True;scene.cycles.texture_limit='512'
        scene.render.resolution_x=600;scene.render.resolution_y=440;scene.render.resolution_percentage=100
        cam=scene.camera;cam.location=(3,-4,2.4)
        cam.rotation_euler=(Vector((0,0,.65))-cam.location).to_track_quat('-Z','Y').to_euler()
        cam.data.type='ORTHO';cam.data.ortho_scale=2.9 if kind!='Elite' else 3.5
        for action in authored:
            rig.animation_data.action=action;rig.animation_data.action_slot=action.slots[0]
            for fraction in ((.0,.3,.55,1.0) if kind=='Hunter' else (1.0,)):
                scene.frame_set(1+round(count*fraction))
                scene.render.filepath=str(ROOT/'Previews'/(action.name+'_'+str(round(fraction*100))+'.png'))
                bpy.ops.render.render(write_still=True)
    print('EXPORTED',kind,flush=True)
(ROOT/'Evidence/authoring.json').write_text(json.dumps(reports,indent=2))
