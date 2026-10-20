"""Original AI-assisted enemy prototypes. Run with Blender, not system Python.
No downloaded mesh or texture inputs. Sources and renders remain outside Unity Assets.
"""
import bpy, math, random, json, hashlib
from pathlib import Path
from mathutils import Vector, Quaternion
import numpy as np

ROOT = Path(__file__).resolve().parent
import sys
sys.path.insert(0, str(ROOT))
from surface_and_deformation import make_material, refine_parts
for sub in ("Blender", "Exports", "Textures", "Previews", "Evidence"):
    (ROOT / sub).mkdir(parents=True, exist_ok=True)
random.seed(241017)
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'
scene.render.engine = 'CYCLES'
scene.cycles.samples = 32
scene.cycles.use_denoising = True
scene.render.resolution_x = 1000
scene.render.resolution_y = 1100
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.world.color = (.12, .12, .12)
scene.view_settings.view_transform = 'AgX'
ASSETS = bpy.data.collections.new("CUSTOM ENEMIES — original meshes")
scene.collection.children.link(ASSETS)
current = None
pieces = []
bones = {}

def move_collection(obj, collection):
    for c in list(obj.users_collection): c.objects.unlink(obj)
    collection.objects.link(obj)

def material(name, color, rough=.85, metal=0, mode="cloth"):
    return make_material(ROOT, name, color, rough, metal, mode)

skin = material("01_Infected_ashen_skin", (.32,.31,.265), .83, mode='skin')
darkskin = material("02_Infected_necrotic_skin", (.23,.26,.22), .88, mode='skin')
wound = material("03_Healed_dark_lesions", (.14,.045,.035), .94, mode='skin')
eye = material("04_Clouded_eyes", (.32,.34,.29), .45, mode='skin')
socket = material("05_Deep_facial_shadows", (.055,.045,.038), .95)
teeth = material("06_Discoloured_teeth", (.48,.43,.28), .8)
ochre = material("07_Hunter_faded_workwear", (.32,.20,.085))
band = material("08_Dirty_reflective_tape", (.45,.43,.26), .65)
runnercloth = material("09_Runner_faded_cotton", (.46,.43,.34))
hoodmat = material("10_Ambusher_rotten_canvas", (.14,.19,.14))
denim = material("11_Filthy_denim", (.115,.15,.17))
leather = material("12_Cracked_boot_leather", (.065,.06,.052), .9)
armour = material("13_Elite_corroded_armour", (.105,.14,.145), .62, .65, 'metal')
rust = material("14_Rusty_fasteners", (.25,.135,.06), .8, .55, 'metal')
black = material("15_Webbing_rubber", (.027,.034,.029), .95)

def finish(obj, name, mat, bone=None):
    obj.name = name
    move_collection(obj, current)
    obj.data.materials.append(mat)
    if bone:
        group = obj.vertex_groups.new(name=bone)
        group.add(list(range(len(obj.data.vertices))), 1, 'REPLACE')
    for poly in obj.data.polygons: poly.use_smooth = True
    pieces.append(obj)
    return obj

def ellipsoid(name, center, scale, mat, bone, segments=20, rings=12):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, location=center)
    obj = bpy.context.object
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return finish(obj, name, mat, bone)

def box(name, center, size, mat, bone, bevel=.012):
    bpy.ops.mesh.primitive_cube_add(size=1, location=center)
    obj = bpy.context.object
    obj.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel:
        mod = obj.modifiers.new("Soft worn edges", 'BEVEL')
        mod.width = bevel
        mod.segments = 2
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return finish(obj, name, mat, bone)

def loft(name, stations, mat, binding, count=20, ragged=False, cap=True):
    # Stations: centre, horizontal radius, depth radius. Ring faces have authored UVs.
    dense = []
    for left, right in zip(stations[:-1], stations[1:]):
        steps = max(1, math.ceil(abs(right[0][2]-left[0][2])/.025))
        for step in range(steps):
            t = step/steps
            dense.append((Vector(left[0]).lerp(Vector(right[0]),t), left[1]*(1-t)+right[1]*t, left[2]*(1-t)+right[2]*t))
    dense.append(stations[-1])
    stations = dense
    vertices, faces = [], []
    for k, (center, rx, ry) in enumerate(stations):
        for j in range(count):
            a = j*2*math.pi/count
            c = Vector(center)
            c += Vector((math.cos(a)*rx, math.sin(a)*ry, 0))
            if ragged and k == 0: c.z += .008*math.sin(j*2.7) + .006*math.cos(j*4.1)
            vertices.append(c)
    for k in range(len(stations)-1):
        for j in range(count):
            faces.append((k*count+j, k*count+(j+1)%count, (k+1)*count+(j+1)%count, (k+1)*count+j))
    if cap:
        faces += [tuple(reversed(range(count))), tuple((len(stations)-1)*count+j for j in range(count))]
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    uv = mesh.uv_layers.new(name='UVMap')
    for poly in mesh.polygons:
        for li in poly.loop_indices:
            v = mesh.loops[li].vertex_index
            uv.data[li].uv = ((v % count)/count, (v//count)/(len(stations)-1))
    obj = bpy.data.objects.new(name, mesh)
    current.objects.link(obj)
    finish(obj, name, mat)
    for v in mesh.vertices:
        weights = binding(v.co) if callable(binding) else {binding:1}
        for b,w in weights.items():
            if w <= 0: continue
            g = obj.vertex_groups.get(b) or obj.vertex_groups.new(name=b)
            g.add([v.index], w, 'REPLACE')
    return obj

def tube(name, start, end, radii, mat, bone, count=14):
    # Joint-centred limb surface with rings normal to its centreline.
    start, end = Vector(start), Vector(end)
    axis = (end-start).normalized()
    u = axis.cross(Vector((0,1,0))).normalized()
    v = axis.cross(u).normalized()
    verts, faces = [], []
    for k,(t,r) in enumerate(radii):
        c = start.lerp(end,t)
        for j in range(count):
            a=j*2*math.pi/count
            verts.append(c + r*(math.cos(a)*u + math.sin(a)*v*.83))
    for k in range(len(radii)-1):
        for j in range(count):
            faces.append((k*count+j,k*count+(j+1)%count,(k+1)*count+(j+1)%count,(k+1)*count+j))
    faces += [tuple(reversed(range(count))),tuple((len(radii)-1)*count+j for j in range(count))]
    mesh=bpy.data.meshes.new(name)
    mesh.from_pydata(verts,[],faces)
    mesh.update()
    uv=mesh.uv_layers.new(name='UVMap')
    for poly in mesh.polygons:
        for li in poly.loop_indices:
            idx=mesh.loops[li].vertex_index
            uv.data[li].uv=((idx%count)/count,(idx//count)/(len(radii)-1))
    obj=bpy.data.objects.new(name,mesh)
    current.objects.link(obj)
    return finish(obj,name,mat,bone)

def blend_z(z, joint, lower, upper, width=.08):
    t=max(0,min(1,(z-joint+width)/(2*width)))
    return {lower:1-t,upper:t}

def add_bone(name, head, tail, parent=None):
    b=rig.data.edit_bones.new(name)
    b.head=head
    b.tail=tail
    if parent: b.parent=rig.data.edit_bones[parent]
    return b

def make_rig(kind, width):
    data=bpy.data.armatures.new(kind+"_HumanoidSkeleton")
    obj=bpy.data.objects.new(kind+"_Rig",data)
    current.objects.link(obj)
    bpy.context.view_layer.objects.active=obj
    obj.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    global rig
    rig=obj
    add_bone("Hips",(0,0,.91),(0,0,1.04))
    add_bone("Spine",(0,0,1.04),(0,0,1.22),"Hips")
    add_bone("Chest",(0,0,1.22),(0,0,1.43),"Spine")
    add_bone("UpperChest",(0,0,1.43),(0,0,1.53),"Chest")
    add_bone("Neck",(0,0,1.53),(0,0,1.65),"UpperChest")
    add_bone("Head",(0,0,1.65),(0,0,1.88),"Neck")
    for side,s in (("Left",1),("Right",-1)):
        hip=.105*width
        sh=.205*width
        elbow=sh+.255
        wrist=elbow+.235
        add_bone(side+"UpperLeg",(s*hip,0,.95),(s*hip,-.025,.52),"Hips")
        add_bone(side+"LowerLeg",(s*hip,-.025,.52),(s*hip,0,.11),side+"UpperLeg")
        add_bone(side+"Foot",(s*hip,0,.11),(s*hip,-.17,.065),side+"LowerLeg")
        add_bone(side+"Toes",(s*hip,-.17,.065),(s*hip,-.24,.065),side+"Foot")
        add_bone(side+"Shoulder",(s*.055,0,1.48),(s*sh,0,1.49),"UpperChest")
        add_bone(side+"UpperArm",(s*sh,0,1.49),(s*elbow,0,1.49),side+"Shoulder")
        add_bone(side+"LowerArm",(s*elbow,0,1.49),(s*wrist,0,1.49),side+"UpperArm")
        add_bone(side+"Hand",(s*wrist,0,1.49),(s*(wrist+.085),0,1.49),side+"LowerArm")
        for f,(name,y,length) in enumerate((("Index",-.032,.076),("Middle",-.009,.087),("Ring",.014,.078),("Little",.035,.059))):
            for seg in range(3):
                x=wrist+.075+length*seg/3
                add_bone(side+name+str(seg+1),(s*x,y,1.49),(s*(x+length/3),y,1.49),
                    side+"Hand" if seg==0 else side+name+str(seg))
        for seg in range(3):
            add_bone(side+"Thumb"+str(seg+1),(s*(wrist+.025+seg*.014),-.04-seg*.018,1.48),
                (s*(wrist+.039+seg*.014),-.058-seg*.018,1.48),
                side+"Hand" if seg==0 else side+"Thumb"+str(seg))
    bpy.ops.object.mode_set(mode='OBJECT')
    obj.show_in_front=True
    obj.select_set(False)
    return obj

def build(kind):
    global current,pieces
    current=bpy.data.collections.new(kind)
    ASSETS.children.link(current)
    pieces=[]
    width={"Hunter":1.03,"Runner":.82,"Ambusher":.91,"Elite":1.30}[kind]
    make_rig(kind,width)
    bodymat=darkskin if kind=="Ambusher" else skin
    # Original anatomical proportions, modelled from rings rather than a vendor character.
    loft("Pelvis",[((0,0,.86),.12*width,.095),((0,0,.94),.155*width,.105),
        ((0,0,1.06),.13*width,.09)],bodymat,"Hips")
    loft("Torso",[((0,0,1.01),.13*width,.09),((0,0,1.15),.125*width,.08),
        ((0,0,1.30),.18*width,.105),((0,0,1.43),.205*width,.105),
        ((0,0,1.50),.17*width,.075),((0,0,1.54),.065,.055)],
        bodymat,lambda p:blend_z(p.z,1.22,"Spine","Chest"))
    tube("Neck",(0,0,1.49),(0,-.014,1.69),[(0,.064),(.6,.057),(1,.058)],bodymat,"Neck")
    loft("Skull",[((0,-.018,1.61),.045,.057),((0,-.026,1.65),.072,.075),
        ((0,-.012,1.70),.087,.083),((0,0,1.77),.099,.088),
        ((0,.009,1.84),.09,.078),((0,.012,1.89),.065,.055),((0,.01,1.91),.015,.016)],
        bodymat,"Head",24)
    for s in (-1,1):
        ellipsoid("Cheekbone",(s*.057,-.064,1.714),(.031,.024,.029),bodymat,"Head")
        ellipsoid("Sunken eye socket",(s*.038,-.083,1.765),(.024,.010,.015),socket,"Head")
        ellipsoid("Clouded recessed eye",(s*.038,-.091,1.766),(.012,.005,.007),eye,"Head",16,8)
        brow=ellipsoid("Heavy brow ridge",(s*.038,-.076,1.787),(.034,.014,.011),bodymat,"Head")
        brow.rotation_euler.y=s*.15
        ellipsoid("Ear",(s*.099,.003,1.745),(.018,.014,.034),bodymat,"Head",16,8)
        ellipsoid("Ear cavity",(s*.108,-.007,1.747),(.008,.008,.021),wound,"Head",12,8)
    ellipsoid("Nasal bridge",(0,-.087,1.746),(.018,.023,.037),bodymat,"Head")
    ellipsoid("Nose tip",(.004,-.107,1.72),(.023,.022,.015),bodymat,"Head")
    ellipsoid("Open mouth",(0,-.084,1.674),(.043,.011,.018),socket,"Head")
    for i in range(7):
        if i==2 and kind=="Runner": continue
        box("Broken tooth",((i-3)*.0105,-.094,1.68+(i%2)*.001),(.008,.008,.012),teeth,"Head",.002)
    ellipsoid("Lower jaw",(0,-.04,1.646),(.065,.054,.021),bodymat,"Head")
    for i in range(3):
        s=-1 if i%2 else 1
        mark=ellipsoid("Scar", (s*(.058+random.random()*.012),-.080,1.70+i*.024),
            (.012,.003,.0035),wound,"Head",12,8)
    for side,s in (("Left",1),("Right",-1)):
        hip=.105*width
        # Shaped knees/calves rather than straight cylindrical legs.
        loft(side+" leg",[((s*hip,0,.105),.038,.045),((s*hip,.012,.25),.055*width,.061),
            ((s*hip,.005,.38),.065*width,.067),((s*hip,-.025,.52),.055*width,.055),
            ((s*hip,-.009,.62),.075*width,.078),((s*hip,0,.81),.088*width,.087),
            ((s*hip,0,.95),.084*width,.083)],bodymat,
            lambda p,side=side:blend_z(p.z,.52,side+"LowerLeg",side+"UpperLeg",.07))
        box(side+" worn boot",(s*hip,-.075,.085),(.12,.28,.12),leather,side+"Foot",.035)
        box(side+" boot sole",(s*hip,-.077,.025),(.125,.29,.040),black,side+"Foot",.015)
        for lace in range(4):
            box(side+" boot lace",(s*hip,-.10+lace*.027,.145),(.066,.008,.006),black,side+"Foot",.002)
        sh=.205*width; elbow=sh+.255; wrist=elbow+.235
        ellipsoid(side+" deltoid",(s*(sh+.015),0,1.47),(.065*width,.068,.058),bodymat,side+"UpperArm")
        tube(side+" upper arm",(s*sh,0,1.49),(s*elbow,0,1.49),
            [(0,.072*width),(.25,.068*width),(.7,.05),(1,.039)],bodymat,side+"UpperArm")
        ellipsoid(side+" elbow",(s*elbow,0,1.49),(.043,.042,.044),bodymat,side+"LowerArm")
        tube(side+" forearm",(s*elbow,0,1.49),(s*wrist,0,1.49),
            [(0,.039),(.3,.047),(.65,.039),(1,.026)],bodymat,side+"LowerArm")
        ellipsoid(side+" palm",(s*(wrist+.042),0,1.49),(.058,.044,.023),bodymat,side+"Hand")
        for name,y,length in (("Index",-.032,.076),("Middle",-.009,.087),("Ring",.014,.078),("Little",.035,.059)):
            for seg in range(3):
                x=wrist+.075+length*seg/3
                tube(side+name+str(seg+1),(s*x,y,1.49),(s*(x+length/3+.002),y,1.489-seg*.001),
                    [(0,.009-seg*.001),(1,.008-seg*.001)],bodymat,side+name+str(seg+1),8)
            box("Dirty fingernail",(s*(wrist+.075+length-.009),y,1.509),(.014,.011,.003),teeth,side+name+"3",.002)
        for seg in range(3):
            tube(side+" thumb",(s*(wrist+.025+seg*.014),-.04-seg*.018,1.48),
                (s*(wrist+.039+seg*.014),-.058-seg*.018,1.48),
                [(0,.011-seg*.001),(1,.010-seg*.001)],bodymat,side+"Thumb"+str(seg+1),8)
        # Trousers conform to the leg and use joint-spanning weights at the knee.
        loft(side+" torn trousers",[((s*hip,.01,.22 if kind=="Runner" and s==-1 else .12),.056*width,.073),
            ((s*hip,.008,.38),.079*width,.090),((s*hip,-.025,.52),.075*width,.078),
            ((s*hip,0,.71),.086*width,.09),((s*hip,0,.95),.095*width,.097)],
            denim if kind!="Elite" else black,
            lambda p,side=side:blend_z(p.z,.52,side+"LowerLeg",side+"UpperLeg",.10),ragged=True)
        if kind=="Elite":
            plate=box(side+" shin guard",(s*hip,-.073,.29),(.12,.045,.29),armour,side+"LowerLeg",.024)
            box(side+" knee armour",(s*hip,-.089,.52),(.12,.052,.10),armour,side+"LowerLeg",.025)
    loft("Trouser waist and seat",[((0,0,.86),.135*width,.106),((0,0,.94),.174*width,.12),
        ((0,0,1.015),.151*width,.104)],denim if kind!="Elite" else black,"Hips")
    shirt=ochre if kind=="Hunter" else runnercloth if kind=="Runner" else hoodmat if kind=="Ambusher" else black
    loft("Torn shirt",[((0,0,.96),.159*width,.112),((0,0,1.12),.145*width,.094),
        ((0,0,1.30),.193*width,.12),((0,0,1.44),.214*width,.12),
        ((0,0,1.50),.18*width,.085),((0,0,1.53),.068,.061)],
        shirt,lambda p:blend_z(p.z,1.22,"Spine","Chest"),ragged=True,cap=False)
    box("Waist belt",(0,-.104,.969),(.27*width,.027,.041),black,"Hips",.008)
    box("Belt buckle",(.025,-.125,.968),(.044,.012,.033),rust,"Hips",.005)
    if kind=="Hunter":
        for side,s in (("Left",1),("Right",-1)):
            sh=.205*width

            tube("Work shirt sleeve",(s*sh,0,1.49),(s*(sh+.20),0,1.49),
                [(0,.080),(.7,.073),(1,.064)],shirt,side+"UpperArm")
            box("Reflective chest strip",(s*.105,-.127,1.36),(.023,.004,.21),band,"Chest",.002)
            box("Stitched chest pocket",(s*.12,-.134,1.29),(.080,.007,.089),shirt,"Chest",.005)
        ellipsoid("Hardhat dome",(0,.01,1.875),(.12,.112,.085),ochre,"Head")
        ellipsoid("Hardhat brim",(0,-.005,1.84),(.145,.14,.013),ochre,"Head")
        box("Helmet centre ridge",(0,-.005,1.947),(.025,.17,.014),band,"Head",.006)
        box("Work belt pouch",(.17,0,.91),(.09,.11,.14),leather,"Hips",.01)
    if kind=="Runner":
        # One sleeve remnant and diagonal torn harness emphasize an asymmetric thin body.
        tube("Ripped sleeve remnant",(-.205*width,0,1.49),(-.205*width-.085,0,1.49),
            [(0,.072),(1,.055)],shirt,"RightUpperArm")
        strap=box("Scavenged diagonal strap",(.01,-.129,1.28),(.035,.018,.46),leather,"Chest",.004)
        strap.rotation_euler.y=-.38

    if kind=="Ambusher":
        # Open-front hood shell. The opening leaves the face visible.
        verts,faces=[],[]
        seg=24
        for row,(z,rx,ry) in enumerate(((1.52,.13,.11),(1.67,.136,.12),(1.83,.13,.12),(1.95,.09,.08),(1.98,.025,.025))):
            for j in range(seg+1):
                a=-.25+math.pi*1.17*j/seg
                verts.append((math.cos(a)*rx, math.sin(a)*ry+.024,z))
        for k in range(4):
            for j in range(seg): faces.append((k*(seg+1)+j,k*(seg+1)+j+1,(k+1)*(seg+1)+j+1,(k+1)*(seg+1)+j))
        mesh=bpy.data.meshes.new("Hood shell")
        mesh.from_pydata(verts,[],faces);mesh.update()
        obj=bpy.data.objects.new("Open torn hood",mesh);current.objects.link(obj)
        finish(obj,obj.name,hoodmat,"Head")
        bpy.context.view_layer.objects.active=obj
        bpy.ops.object.select_all(action='DESELECT');obj.select_set(True)
        bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project();bpy.ops.object.mode_set(mode='OBJECT')
        solid=obj.modifiers.new("Canvas thickness",'SOLIDIFY');solid.thickness=.006
        bpy.ops.object.modifier_apply(modifier=solid.name)
        # Rear half cape, individually broken hem.
        loft("Ragged shoulder mantle",[((0,.038,1.11),.23,.13),((0,.02,1.39),.26,.14),
            ((0,0,1.51),.16,.09)],hoodmat,"Chest",ragged=True,cap=False)
        for s in (-1,1):
            tube("Hood drawstring",(s*.065,-.075,1.59),(s*.085,-.14,1.35),
                [(0,.004),(1,.004)],black,"Chest",8)
    if kind=="Elite":
        box("Corroded breastplate",(0,-.145,1.30),(.38,.042,.36),armour,"Chest",.035)
        for x in (-.16,.16):
            for z in (1.18,1.42):
                ellipsoid("Armour rivet",(x,-.182,z),(.012,.007,.012),rust,"Chest",12,8)
        for i in range(3):
            box("Abdominal armour segment",(0,-.132,1.04+i*.065),(.31-i*.025,.048,.052),armour,"Spine",.008)
        for side,s in (("Left",1),("Right",-1)):
            sh=.205*width
            ellipsoid("Asymmetric shoulder plate",(s*(sh+.035),0,1.51),(.102,.09,.058),armour,side+"UpperArm")
            box("Heavy belt pouch",(s*.17,-.08,.98),(.10,.10,.13),black,"Hips",.012)
        ellipsoid("Battered riot helmet",(0,.014,1.86),(.12,.112,.10),armour,"Head")
        box("Raised helmet brow",(0,-.102,1.82),(.22,.04,.041),armour,"Head",.012)
        for i in range(3):
            box("Helmet scar",(.05+i*.017,-.121,1.829),(.007,.007,.024),rust,"Head",.001)
    # Fuse facial skin pieces into a single continuous surface.
    face=[o for o in pieces if o.vertex_groups.get("Head") is not None and o.data.materials[0] == bodymat]
    bpy.ops.object.select_all(action='DESELECT')
    for o in face: o.select_set(True)
    bpy.context.view_layer.objects.active=face[0]
    for o in face: pieces.remove(o)
    bpy.ops.object.join()
    head=bpy.context.object
    head.name="Unified sculpted head"
    rem=head.modifiers.new("Fuse facial anatomy",'REMESH');rem.mode='VOXEL';rem.voxel_size=.0035
    bpy.ops.object.modifier_apply(modifier=rem.name)
    smooth=head.modifiers.new("Soften anatomical joins",'SMOOTH');smooth.factor=.6;smooth.iterations=3
    bpy.ops.object.modifier_apply(modifier=smooth.name)
    dec=head.modifiers.new("Game mesh reduction",'DECIMATE');dec.ratio=.38
    bpy.ops.object.modifier_apply(modifier=dec.name)
    head.vertex_groups.clear()
    g=head.vertex_groups.new(name="Head");g.add(list(range(len(head.data.vertices))),1,'REPLACE')
    for p in head.data.polygons:p.use_smooth=True
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project();bpy.ops.object.mode_set(mode='OBJECT')
    pieces.append(head)
    refine_parts(pieces, current, rig, width, kind, bodymat)
    # Join the mesh while retaining separate named vertex groups and material slots.
    bpy.ops.object.select_all(action='DESELECT')
    for obj in pieces: obj.select_set(True)
    bpy.context.view_layer.objects.active=pieces[0]
    bpy.ops.object.join()
    mesh=bpy.context.object
    mesh.name=kind+"_CustomMesh"
    mesh.data.validate(verbose=True)
    mesh.data.update()
    mesh.parent=rig
    mod=mesh.modifiers.new("Humanoid deformation",'ARMATURE');mod.object=rig
    rig["provenance"]="Original procedural modelling by Codex in Blender; AI-assisted, not student manual authorship."
    rig["review_status"]="Revision 2: continuous skin and trouser surfaces, PBR textures; animation preview required."
    rig["enemy_role"]={"Hunter":"Persistent workwear tracker","Runner":"Lean opportunistic sprinter",
        "Ambusher":"Concealed hooded stalker","Elite":"Armoured heavyweight"}[kind]
    bpy.ops.object.select_all(action='DESELECT');mesh.select_set(True);rig.select_set(True)
    bpy.context.view_layer.objects.active=rig
    rig.scale=(1,1,1) if kind!="Elite" else (1.07,1.07,1.07)
    # Export a neutral T pose suitable for later mapping; no studio objects.
    bpy.ops.export_scene.fbx(filepath=str(ROOT/"Exports"/(kind+".fbx")),use_selection=True,
        add_leaf_bones=False,bake_anim=False,object_types={'ARMATURE','MESH'},
        apply_unit_scale=True,axis_forward='-Z',axis_up='Y',path_mode='COPY',embed_textures=True)
    bpy.ops.export_scene.gltf(filepath=str(ROOT/"Exports"/(kind+".glb")),export_format='GLB',
        use_selection=True,export_animations=False,export_yup=True)
    # Preview pose: arms lower, slight hunched chest; no claimed walking animation.
    def rotate_world(name, axis, angle):
        p=rig.pose.bones[name];r=p.bone.matrix_local.to_quaternion()
        p.rotation_mode='QUATERNION'
        p.rotation_quaternion=r.inverted() @ Quaternion(Vector(axis),math.radians(angle)) @ r
    for side,s in (("Left",1),("Right",-1)):
        rotate_world(side+"UpperArm",(0,1,0),s*{"Hunter":62,"Runner":58,"Ambusher":67,"Elite":55}[kind])
        rotate_world(side+"LowerArm",(0,0,1),s*-18)
    rotate_world("Chest",(1,0,0),{"Hunter":5,"Runner":10,"Ambusher":13,"Elite":3}[kind])
    rotate_world("Head",(1,0,0),-5)
    rig.scale=(1,1,1) if kind!="Elite" else (1.07,1.07,1.07)
    rig.location=(0,0,0)
    bpy.context.view_layer.update()
    return rig,mesh,current

models=[]
for kind in ("Hunter","Runner","Ambusher","Elite"):
    models.append(build(kind))

# Studio is its own collection so all game meshes can be exported independently.
studio=bpy.data.collections.new("PREVIEW STUDIO — not exported")
scene.collection.children.link(studio)
bpy.ops.mesh.primitive_plane_add(size=200)
floor=bpy.context.object;floor.name="Studio floor";move_collection(floor,studio)
floorMat=bpy.data.materials.new("Studio charcoal");floorMat.diffuse_color=(.055,.065,.065,1);floor.data.materials.append(floorMat)
def light(name,loc,power,size,color):
    data=bpy.data.lights.new(name,'AREA');data.energy=power;data.shape='DISK';data.size=size;data.color=color
    obj=bpy.data.objects.new(name,data);studio.objects.link(obj);obj.location=loc
    obj.rotation_euler=(Vector((0,0,1))-obj.location).to_track_quat('-Z','Y').to_euler()
light("Large warm key",(-3,-4,5),500,4,(1,.86,.70))
light("Soft cool fill",(3,-2,3),220,3,(.68,.80,1))
light("Edge light",(1,3,4),650,3,(.78,.90,1))
camdata=bpy.data.cameras.new("Preview camera")
cam=bpy.data.objects.new("Preview camera",camdata);studio.objects.link(cam)
scene.camera=cam;camdata.type='ORTHO'
def camera(location,target,scale):
    cam.location=location;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler()
    camdata.ortho_scale=scale
def visible(collection,value):
    collection.hide_render=not value
    collection.hide_viewport=not value
report=[]
for rig,mesh,col in models:
    for _,_,other in models: visible(other,other==col)
    kind=col.name
    camera((2.7,-5,2.5),(0,0,1.03),2.25 if kind!="Elite" else 2.45)
    scene.render.filepath=str(ROOT/"Previews"/(kind+"_front.png"))
    bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/"Blender"/(kind+".blend")))
    if '--quick' not in sys.argv: bpy.ops.render.render(write_still=True)
    camera((-2.8,4.8,2.3),(0,0,1.03),2.25 if kind!="Elite" else 2.45)
    scene.render.filepath=str(ROOT/"Previews"/(kind+"_back.png"))
    if '--quick' not in sys.argv: bpy.ops.render.render(write_still=True)
    mesh.data.calc_loop_triangles()
    unweighted=sum(1 for v in mesh.data.vertices if not v.groups)
    report.append(dict(name=kind,vertices=len(mesh.data.vertices),triangles=len(mesh.data.loop_triangles),
        bones=len(rig.data.bones),unweighted_vertices=unweighted,uv_layers=len(mesh.data.uv_layers),
        materials=len(mesh.data.materials),rig_status="Unmapped humanoid prototype; deformation review pending"))

for i,(rig,mesh,col) in enumerate(models):
    visible(col,True);rig.location.x=(i-1.5)*1.25
camera((3,-11,4),(0,0,1.04),5.5)
scene.render.resolution_x=1800;scene.render.resolution_y=850
scene.render.filepath=str(ROOT/"Previews"/"Four_Enemies_Lineup.png")
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/"Blender"/"Four_Enemies_Review.blend"))
bpy.ops.render.render(write_still=True)
(ROOT/"Evidence"/"mesh_report.json").write_text(json.dumps(report,indent=2))
hashes={}
for path in ROOT.rglob("*"):
    if path.is_file() and path.suffix in (".blend",".fbx",".glb",".png",".py"):
        hashes[str(path.relative_to(ROOT))]=hashlib.sha256(path.read_bytes()).hexdigest()
(ROOT/"Evidence"/"sha256.json").write_text(json.dumps(hashes,indent=2))
print("CUSTOM_ENEMIES_COMPLETE",json.dumps(report))


