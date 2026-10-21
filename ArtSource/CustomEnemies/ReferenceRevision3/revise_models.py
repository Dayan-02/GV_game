"""Reference-directed revision of the project's original procedural characters.

Run with Blender. Reuses original skeleton definitions and saved animation actions.
Writes only ReferenceRevision3; never modifies Unity assets or earlier .blend files.
Reference-projected colour is concept look development, not a seamless PBR atlas.
"""
import bpy
import bmesh
import math
import json
import hashlib
import random
import sys
import numpy as np
from pathlib import Path
from mathutils import Vector, Quaternion

ROOT = Path(__file__).resolve().parent
BASE = ROOT.parent
REFS = BASE / "ConceptTurnarounds-20261020"
SOURCE = BASE / "BlenderAnimationPass" / "Blender"
for folder in ("Blender", "Previews", "Evidence"):
    (ROOT / folder).mkdir(exist_ok=True)
sys.path.insert(0, str(BASE / "Revision2"))
from surface_and_deformation import refine_parts as old_refine
from surface_and_deformation import reweight, weights


def make_material(root, name, color, rough, metal, mode):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    mat.diffuse_color = (*color, 1)
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    bs = nodes.get("Principled BSDF")
    bs.inputs["Base Color"].default_value = (*color, 1)
    bs.inputs["Roughness"].default_value = rough
    bs.inputs["Metallic"].default_value = metal
    tex = nodes.new("ShaderNodeTexNoise")
    tex.inputs["Scale"].default_value = 140 if mode == "skin" else 190
    tex.inputs["Detail"].default_value = 3
    bump = nodes.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = .18
    bump.inputs["Distance"].default_value = .001 if mode == "skin" else .0015
    links.new(tex.outputs["Fac"], bump.inputs["Height"])
    links.new(bump.outputs["Normal"], bs.inputs["Normal"])
    if mode == "skin":
        bs.inputs["Subsurface Weight"].default_value = .035
        bs.inputs["Subsurface Radius"].default_value = (.7, .3, .15)
    if mode == "metal":
        coarse=nodes.new("ShaderNodeTexNoise");coarse.inputs["Scale"].default_value=18
        coarse.inputs["Detail"].default_value=4
        ramp=nodes.new("ShaderNodeValToRGB")
        ramp.color_ramp.elements[0].position=.34
        ramp.color_ramp.elements[0].color=(.045,.052,.055,1)
        ramp.color_ramp.elements[1].position=.64
        ramp.color_ramp.elements[1].color=(.23,.095,.034,1)
        links.new(coarse.outputs["Fac"],ramp.inputs[0])
        links.new(ramp.outputs[0],bs.inputs["Base Color"])
    return mat


def refine_parts(pieces, collection, rig, width, kind, skin):
    print("REFINE_BEGIN",kind,len(pieces),flush=True)
    # Keep full lower legs for the new shorts. Old refinement deliberately removed them.
    saved = []
    if kind == "Runner":
        for obj in list(pieces):
            if obj.name.split(".")[0] in ("Left leg", "Right leg"):
                bm = bmesh.new()
                bm.from_mesh(obj.data)
                bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.z > .75],
                                 context="VERTS")
                bm.to_mesh(obj.data)
                bm.free()
                pieces.remove(obj)
                saved.append(obj)
    old_refine(pieces, collection, rig, width, kind, skin)
    pieces.extend(saved)
    print("REFINE_COMPLETE",kind,sum(len(o.data.vertices) for o in pieces),flush=True)


def prepare_source():
    text = (BASE / "Revision2" / "build_enemies.py").read_text()
    text = text[text.index("def move_collection"):text.index("\nmodels=[]")]
    # Use the existing construction functions, with denser curved surfaces.
    text = text.replace("segments=20, rings=12", "segments=32, rings=20")
    text = text.replace("count=20, ragged=False", "count=32, ragged=False")
    text = text.replace("binding, count=32", "binding, count=32")
    text = text.replace("bone, count=14", "bone, count=24")
    text = text.replace("    start, end = Vector(start), Vector(end)\n",
                        "    radii = densify(radii)\n    start, end = Vector(start), Vector(end)\n")
    # Earthy brown workwear, charcoal athletic wear and hood; orange industrial pants.
    text = text.replace("(.32,.20,.085)", "(.18,.125,.075)")
    text = text.replace("(.46,.43,.34)", "(.045,.053,.060)")
    text = text.replace("(.14,.19,.14)", "(.052,.048,.044)")
    text = text.replace("denim if kind!=\"Elite\" else black",
                        "denim if kind!='Elite' else orange")
    text = text.replace("((0,.009,1.84),.09,.078)", "((0,.009,1.84),.087,.077)")
    text = text.replace("((0,.012,1.89),.065,.055)", "((0,.012,1.89),.059,.052)")
    text = text.replace("(.024,.010,.015)", "(.021,.008,.010)")
    text = text.replace("(.012,.005,.007)", "(.010,.005,.0055)")
    text = text.replace('box(side+" worn boot",(s*hip,-.075,.085),(.12,.28,.12),leather,side+"Foot",.035)',
                        'ellipsoid(side+" worn boot",(s*hip,-.075,.085),(.067,.145,.064),leather,side+"Foot",32,20)')
    # Eliminate accessories absent from the approved reference.
    begin = text.index('        ellipsoid("Hardhat dome"')
    end = text.index('    if kind=="Runner":', begin)
    text = text[:begin] + text[end:]
    begin = text.index('        # One sleeve remnant')
    end = text.index('    if kind=="Ambusher":', begin)
    text = text[:begin] + "        pass\n\n" + text[end:]
    begin = text.index('        ellipsoid("Battered riot helmet"')
    end = text.index('    # Fuse facial skin', begin)
    text = text[:begin] + text[end:]
    text = text.replace('            box("Reflective chest strip",(s*.105,-.127,1.36),(.023,.004,.21),band,"Chest",.002)', "")
    text = text.replace('(s*(sh+.20),0,1.49)', '(s*(sh+.365),0,1.49)')
    text = text.replace('[(0,.080),(.7,.073),(1,.064)]', '[(0,.083),(.35,.078),(.7,.060),(1,.052)]')
    # Shorts terminate above the knee with real visible calf geometry.
    text = text.replace('.22 if kind=="Runner" and s==-1 else .12',
                        '.69 if kind=="Runner" else .12')
    text = text.replace('((s*hip,.008,.38),.079*width,.090)',
                        '((s*hip,.008,.74 if kind=="Runner" else .38),.079*width,.090)')
    text = text.replace('((s*hip,-.025,.52),.075*width,.078)',
                        '((s*hip,-.012,.79 if kind=="Runner" else .52),.075*width,.078)')
    text = text.replace('((s*hip,0,.71),.086*width,.09)',
                        '((s*hip,0,.85 if kind=="Runner" else .71),.086*width,.09)')
    text = text.replace('((0,.038,1.11),.23,.13)',
                        '((0,.038,.86),.21,.135)')
    text = text.replace('((0,.02,1.39),.26,.14)',
                        '((0,.02,1.39),.215,.135)')
    # Remove floating shoulder caps and shin guards. Keep articulated kneepads.
    text = "\n".join(line for line in text.splitlines()
                     if 'plate=box(side+" shin guard"' not in line
                     and 'ellipsoid("Asymmetric shoulder plate"' not in line)
    text = text.replace('(.38,.042,.36)', '(.43,.045,.32)')
    text = text.replace('    # Export a neutral T pose suitable for later mapping; no studio objects.\n',
                        '    # Export deferred until visual review.\n')
    begin = text.index('    bpy.ops.export_scene.fbx')
    end = text.index('    # Preview pose:', begin)
    text = text[:begin] + text[end:]
    # Preserve the original bone definitions exactly; remeshed facial surface detail increases.
    text = text.replace("rem.voxel_size=.0035", "rem.voxel_size=.0025")
    text = text.replace("dec.ratio=.38", "dec.ratio=.55")
    # Different skin colours are also replaced by reference materials later.
    text = text.replace("    refine_parts(pieces, current, rig, width, kind, bodymat)",
                        "    extra_anatomy(kind, width, bodymat)\n    refine_parts(pieces, current, rig, width, kind, bodymat)")
    text = text.replace("    # Join the mesh while retaining", "    reference_details(kind, width, bodymat)\n    # Join the mesh while retaining")
    return text


def densify(radii):
    result = []
    for (ta, ra), (tb, rb) in zip(radii, radii[1:]):
        for i in range(4):
            t = i / 4
            result.append((ta + (tb-ta)*t, ra + (rb-ra)*t))
    return result + [radii[-1]]


def extra_anatomy(kind, width, mat):
    # These parts are fused with the original skin, never left as floating muscle balls.
    for side, s in (("Left", 1), ("Right", -1)):
        sh = .205 * width
        tube("Integrated shoulder slope",(s*.075,0,1.485),(s*(sh+.025),0,1.46),
             [(0,.056),(.5,.067),(1,.061)],mat,"UpperChest",24)
        if kind in ("Hunter","Ambusher"):
            cloth=ochre if kind=="Hunter" else hoodmat
            tube("Work shirt sleeve shoulder",(s*.085,0,1.485),(s*(sh+.035),0,1.46),
                 [(0,.062),(.5,.076),(1,.072)],cloth,"UpperChest",24)
        if kind == "Elite":
            ellipsoid("Trapezius volume", (s*.13, .012, 1.48), (.14,.091,.085), mat, "UpperChest")
            ellipsoid("Integrated deltoid", (s*(sh+.03), 0, 1.49), (.105,.102,.092), mat, side+"UpperArm")
            ellipsoid("Integrated biceps", (s*(sh+.135), -.018, 1.485), (.13,.084,.082), mat, side+"UpperArm")
            ellipsoid("Integrated forearm", (s*(sh+.35), .005, 1.49), (.095,.060,.059), mat, side+"LowerArm")


def curve_mesh(name, points, radius, mat, bone):
    cu = bpy.data.curves.new(name, "CURVE")
    cu.dimensions = "3D"
    cu.resolution_u = 3
    cu.bevel_depth = radius
    cu.bevel_resolution = 2
    spl = cu.splines.new("BEZIER")
    spl.bezier_points.add(len(points)-1)
    for i, (point, co) in enumerate(zip(spl.bezier_points, points)):
        point.co = co
        point.handle_left_type = "AUTO"
        point.handle_right_type = "AUTO"
        point.radius = 1 - .85*i/max(1, len(points)-1)
    obj = bpy.data.objects.new(name, cu)
    current.objects.link(obj)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.convert(target="MESH")
    return finish(bpy.context.object, name, mat, bone)


def cloth_patch(name, coordinates, mat, bone):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(coordinates, [], [tuple(range(len(coordinates)))])
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    current.objects.link(obj)
    finish(obj, name, mat, bone)
    return obj


def reference_details(kind, width, mat):
    print("DETAILS_BEGIN",kind,flush=True)
    # Remove the old separate toy-like eye/mouth inserts. The new facial surface
    # carries the reference expression without an extra set of eyes or teeth.
    for obj in list(pieces):
        if any(obj.name.startswith(n) for n in ("Sunken eye socket","Clouded recessed eye",
                                                "Open mouth","Broken tooth")):
            pieces.remove(obj);bpy.data.objects.remove(obj,do_unlink=True)
    for side,s in (("Left",1),("Right",-1)):
        hip=.105*width
        loft("Boot ankle collar", [((s*hip,0,.10),.056,.062),
             ((s*hip,0,.13 if kind=="Runner" else .16),.051,.055),
             ((s*hip,0,.15 if kind=="Runner" else .20),.049,.051)],
             leather,side+"Foot",count=24,cap=False)
    if kind in ("Hunter", "Runner"):
        # Layered locks with a dark scalp; mesh geometry remains portable and editable.
        ellipsoid("Hair scalp", (0,.018,1.856), (.086,.075,.058), hair, "Head", 32, 20)
        rng = random.Random(87 if kind == "Hunter" else 91)
        for i in range(150):
            angle = rng.uniform(0, math.tau)
            x, y = math.cos(angle)*.085, math.sin(angle)*.073+.012
            root = (x*.4, y*.4, rng.uniform(1.885,1.922))
            middle = (x*1.2+rng.uniform(-.02,.02), y*1.2, rng.uniform(1.84,1.90))
            tip = (x+rng.uniform(-.035,.035), y+rng.uniform(-.025,.025), rng.uniform(1.73,1.84))
            curve_mesh("Matted hair lock", [root, middle, tip], rng.uniform(.0013,.0028), hair, "Head")
    if kind == "Hunter":
        for s in (-1,1):
            cloth_patch("Folded workshirt collar",
                        [(s*.045,-.071,1.53),(s*.105,-.111,1.47),(s*.052,-.14,1.40)],
                        ochre, "UpperChest")
            box("Pocket flap", (s*.12,-.143,1.335), (.087,.011,.023), ochre, "Chest", .004)
        # The narrow placket distinguishes an open workshirt from a solid uniform.
        box("Dark shirt opening", (0,-.125,1.25), (.042,.006,.30), undershirt, "Chest", .002)
        for i in range(6):
            ellipsoid("Workshirt button", (.027,-.131,1.08+i*.062), (.004,.002,.004), rust, "Chest", 10, 6)
    if kind == "Runner":
        for side,s in (("Left",1),("Right",-1)):
            hip=.105*width
            for offset in (0,.012):
                curve_mesh("Sports shorts side seam", [(s*(hip+.081),.005+offset,.92),
                           (s*(hip+.080),.005+offset,.74),(s*(hip+.059),.005+offset,.605)],
                           .003, stripe, side+"UpperLeg")
    if kind == "Ambusher":
        for side,s in (("Left",1),("Right",-1)):
            sh=.205*width
            tube("Tattered long sleeve",(s*sh,0,1.49),(s*(sh+.40),0,1.49),
                 [(0,.083),(.3,.076),(.7,.057),(1,.044)],hoodmat,side+"UpperArm")
            # Sleeve blends across the elbow when joining the final mesh.
            sleeve=pieces[-1]
            reweight(sleeve,width)
        rng=random.Random(193)
        for i in range(28):
            angle=math.tau*i/28
            x,y=math.cos(angle)*.19,math.sin(angle)*.122
            z=.95+rng.uniform(-.06,.07)
            cloth_patch("Torn coat hem",
                        [(x-.025,y,z+.12),(x+.025,y,z+.12),
                         (x+.015,y+.008,z-rng.uniform(.06,.20)),(x-.013,y,z-.05)],
                        hoodmat,"Hips")
        for s in (-1,1):
            curve_mesh("Hood folded rim",[(s*.025,.005,1.97),(s*.11,-.025,1.87),
                       (s*.115,-.035,1.69),(s*.08,-.06,1.54)],.009,hoodmat,"Head")
        curve_mesh("Weathered diagonal strap", [(-.14,-.122,1.48),(0,-.148,1.27),
                   (.14,-.126,1.06)],.013,leather,"Chest")
        for z in (.967,.978,.989):
            curve_mesh("Waist rope", [(-.18,0,z),(0,-.132,z),(.18,0,z),(0,.135,z),(-.18,0,z)],
                       .004,rope,"Hips")
    if kind == "Elite":
        box("Back armour carrier",(0,.137,1.30),(.43,.043,.32),armour,"Chest",.028)
        for side,s in (("Left",1),("Right",-1)):
            for y in (-.16,.157):
                box("Vest vertical webbing",(s*.16,y,1.31),(.032,.014,.34),black,"Chest",.004)
                for z in (1.2,1.27,1.34):
                    box("Vest utility pocket",(s*.105,y*1.16,z),(.085,.03,.055),leather,"Chest",.006)
            curve_mesh("Armour shoulder strap",[(s*.15,-.16,1.45),(s*.16,0,1.54),
                       (s*.15,.155,1.45)],.022,black,"UpperChest")
            hip=.105*width
            box("Industrial cargo pocket",(s*(hip+.083),.006,.75),(.048,.13,.15),orange,side+"UpperLeg",.011)
            sh=.205*width
            box("Glove back",(s*(sh+.53),.003,1.514),(.10,.083,.028),leather,side+"Hand",.010)
        for i in range(5):
            curve_mesh("Brow scar",[(.02+i*.011,-.075,1.82),(.014+i*.011,-.083,1.80),
                       (.016+i*.011,-.086,1.78)],.0017,wound,"Head")


def projected_point(p, width):
    """Map the existing T-rest arms to the guide's A-pose for colour projection."""
    p = p.copy()
    sh=.205*width
    if abs(p.x)>sh-.025 and 1.32<p.z<1.60:
        s=1 if p.x>0 else -1
        pivot=Vector((s*sh,0,1.49))
        p=pivot + Quaternion((0,1,0),s*math.radians(61)) @ (p-pivot)
    return p


GUIDES={
    "Hunter":dict(centers=(255,680,1136,1554),baseline=800,ppm=382,face=(9,5),feet=90,knee=65),
    "Runner":dict(centers=(253,683,1132,1552),baseline=800,ppm=378,face=(11,14),feet=85,knee=61),
    "Ambusher":dict(centers=(250,665,1130,1541),baseline=800,ppm=383,face=(1,40),feet=85,knee=61),
    "Elite":dict(centers=(263,685,1120,1548),baseline=800,ppm=397,face=(13,-9),feet=108,knee=78),
}


def assign_reference_materials(mesh, kind, width):
    guide=GUIDES[kind]
    image=bpy.data.images.load(str(REFS/(kind+".png")),check_existing=True)
    image.pack()
    iw,ih=image.size
    # Reference sheets are 1774x887 previews; use normalized coordinates for native size.
    sx,sy=iw/1774,ih/887
    layers=[mesh.data.uv_layers.new(name="Guide_"+n) for n in ("Front","Back","Left","Right")]
    color=mesh.data.color_attributes.new(name="GuideBlend",type="FLOAT_COLOR",domain="CORNER")
    normal_matrix=mesh.matrix_world.to_3x3()
    # Cache normals before writing any corner data. Interleaving normal reads and
    # UV edits invalidates Blender's normal cache and can turn this into O(n^2).
    normals=[(normal_matrix@v.normal).normalized() for v in mesh.data.vertices]
    points=[projected_point(mesh.matrix_world@v.co,width) for v in mesh.data.vertices]
    facial_vertices=set()
    for poly in mesh.data.polygons:
        if mesh.data.materials[poly.material_index].name.startswith(("01_","02_")):
            facial_vertices.update(poly.vertices)
    vert_uv=[[] for _ in layers]
    vert_colors=[]
    for vertex_index,(p,normal) in enumerate(zip(points,normals)):
        front=max(0,-normal.y)**3
        back=max(0,normal.y)**3
        left=max(0,normal.x)**5*.35
        right=max(0,-normal.x)**5*.35
        total=max(.001,front+back+left+right)
        vert_colors.append((front/total,back/total,left/total,1))
        # The guides have a wider A-stance than the original straight rig.
        # Remap each lower limb around its own centre, preserving its width.
        lateral=p.x*guide["ppm"]
        if p.z<.94:
            spread=float(np.interp(p.z,[0,.52,.94],[guide["feet"],guide["knee"],.105*width*guide["ppm"]]))
            lateral+=(1 if p.x>0 else -1)*(spread-.105*width*guide["ppm"])
        face_weight=max(0,min(1,(p.z-1.57)/.09)) if vertex_index in facial_vertices else 0
        coords=(guide["centers"][0]+lateral+guide["face"][0]*face_weight,
                guide["centers"][2]-lateral,
                guide["centers"][1]+p.y*guide["ppm"],
                guide["centers"][3]-p.y*guide["ppm"])
        for uv_index,(uvs,u) in enumerate(zip(vert_uv,coords)):
            pixel_y=guide["baseline"]-p.z*guide["ppm"]
            if uv_index==0:pixel_y+=guide["face"][1]*face_weight
            uvs.append((u*sx/iw,1-pixel_y*sy/ih))
    loop_indices=np.empty(len(mesh.data.loops),dtype=np.int32)
    mesh.data.loops.foreach_get("vertex_index",loop_indices)
    color.data.foreach_set("color",np.asarray(vert_colors,dtype=np.float32)[loop_indices].ravel())
    for layer,uvs in zip(layers,vert_uv):
        layer.data.foreach_set("uv",np.asarray(uvs,dtype=np.float32)[loop_indices].ravel())
    for index,old in enumerate(list(mesh.data.materials)):
        # Small independent details use their original physically based materials.
        if old.name.startswith(("03_","04_","05_","06_","13_","14_","15_","R3_Hair","R3_Rope","R3_Stripe")):
            continue
        mat=old.copy()
        mat.name=kind+"_Reference_"+old.name
        nodes,links=mat.node_tree.nodes,mat.node_tree.links
        bs=nodes.get("Principled BSDF")
        attr=nodes.new("ShaderNodeVertexColor");attr.layer_name="GuideBlend"
        sep=nodes.new("ShaderNodeSeparateColor");links.new(attr.outputs["Color"],sep.inputs["Color"])
        texs=[]
        for layer in layers:
            uv=nodes.new("ShaderNodeUVMap");uv.uv_map=layer.name
            tex=nodes.new("ShaderNodeTexImage");tex.image=image;tex.interpolation="Linear"
            links.new(uv.outputs["UV"],tex.inputs["Vector"]);texs.append(tex)
        # Weight the two primary front/back projections; side replaces only grazing surfaces.
        add=nodes.new("ShaderNodeMath");add.operation="ADD"
        links.new(sep.outputs[0],add.inputs[0]);links.new(sep.outputs[1],add.inputs[1])
        den=nodes.new("ShaderNodeMath");den.operation="MAXIMUM";den.inputs[1].default_value=.001
        links.new(add.outputs[0],den.inputs[0])
        ratio=nodes.new("ShaderNodeMath");ratio.operation="DIVIDE"
        links.new(sep.outputs[1],ratio.inputs[0]);links.new(den.outputs[0],ratio.inputs[1])
        blend=nodes.new("ShaderNodeMixRGB");blend.blend_type="MIX"
        links.new(ratio.outputs[0],blend.inputs[0])
        links.new(texs[0].outputs["Color"],blend.inputs[1]);links.new(texs[1].outputs["Color"],blend.inputs[2])
        # Neutral reference background must not turn outlying geometry white.
        hsv=nodes.new("ShaderNodeSeparateColor");hsv.mode="HSV";links.new(blend.outputs[0],hsv.inputs[0])
        grey=nodes.new("ShaderNodeMath");grey.operation="LESS_THAN";grey.inputs[1].default_value=.10
        links.new(hsv.outputs[1],grey.inputs[0])
        bright=nodes.new("ShaderNodeMath");bright.operation="GREATER_THAN";bright.inputs[1].default_value=.24
        links.new(hsv.outputs[2],bright.inputs[0])
        mask=nodes.new("ShaderNodeMath");mask.operation="MULTIPLY"
        links.new(grey.outputs[0],mask.inputs[0]);links.new(bright.outputs[0],mask.inputs[1])
        safe=nodes.new("ShaderNodeMixRGB")
        links.new(mask.outputs[0],safe.inputs[0]);links.new(blend.outputs[0],safe.inputs[1])
        safe.inputs[2].default_value=old.diffuse_color
        links.new(safe.outputs[0],bs.inputs["Base Color"])
        mesh.data.materials[index]=mat
    mesh["reference_texture_note"]="AI concept sheet projected into custom UV layers. Contains painted lighting and cross-view seams; not a final seamless PBR atlas."


def clear_pose(rig):
    rig.animation_data_clear()
    for bone in rig.pose.bones:
        bone.rotation_mode="QUATERNION"
        bone.rotation_quaternion=(1,0,0,0)
        bone.location=(0,0,0)
        bone.scale=(1,1,1)


def hand_material(mesh,kind,width):
    """Give palms/fingers a local projection instead of sampling grey guide margins."""
    uv=mesh.data.uv_layers.get("HandDetail") or mesh.data.uv_layers.new(name="HandDetail")
    image=bpy.data.images.get(kind+".png")
    if image is None:
        image=bpy.data.images.load(str(REFS/(kind+".png")),check_existing=True)
    image.pack()
    mat=make_material(ROOT,kind+"_HandDetail",
                      (.23,.16,.115) if kind!="Elite" else (.035,.027,.018),
                      .87,0,"skin" if kind!="Elite" else "cloth")
    nodes,links=mat.node_tree.nodes,mat.node_tree.links
    coord=nodes.new("ShaderNodeUVMap");coord.uv_map="HandDetail"
    tex=nodes.new("ShaderNodeTexImage");tex.image=image
    links.new(coord.outputs["UV"],tex.inputs["Vector"])
    bs=nodes.get("Principled BSDF")
    hsv=nodes.new("ShaderNodeSeparateColor");hsv.mode="HSV"
    links.new(tex.outputs["Color"],hsv.inputs[0])
    neutral=nodes.new("ShaderNodeMath");neutral.operation="LESS_THAN";neutral.inputs[1].default_value=.20
    links.new(hsv.outputs[1],neutral.inputs[0])
    bright=nodes.new("ShaderNodeMath");bright.operation="GREATER_THAN";bright.inputs[1].default_value=.18
    links.new(hsv.outputs[2],bright.inputs[0])
    mask=nodes.new("ShaderNodeMath");mask.operation="MULTIPLY"
    links.new(neutral.outputs[0],mask.inputs[0]);links.new(bright.outputs[0],mask.inputs[1])
    noise=nodes.new("ShaderNodeTexNoise");noise.inputs["Scale"].default_value=35
    noise.inputs["Detail"].default_value=3
    ramp=nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].color=(.11,.070,.05,1) if kind!="Elite" else (.012,.014,.012,1)
    ramp.color_ramp.elements[1].color=(.29,.21,.16,1) if kind!="Elite" else (.055,.043,.029,1)
    links.new(noise.outputs["Fac"],ramp.inputs[0])
    safe=nodes.new("ShaderNodeMixRGB")
    links.new(mask.outputs[0],safe.inputs[0]);links.new(tex.outputs["Color"],safe.inputs[1])
    links.new(ramp.outputs[0],safe.inputs[2]);links.new(safe.outputs[0],bs.inputs["Base Color"])
    mesh.data.materials.append(mat);slot=len(mesh.data.materials)-1
    local=mesh.parent.matrix_world.inverted()@mesh.matrix_world
    positions=[local@v.co for v in mesh.data.vertices]
    wrist=.205*width+.49
    center={"Hunter":435,"Runner":437,"Ambusher":430,"Elite":473}[kind]
    for poly in mesh.data.polygons:
        pp=[positions[i] for i in poly.vertices]
        # Isolate the original T-rest wrists/hands using coordinates, including fingers.
        if not all(abs(p.x)>wrist-.025 and 1.40<p.z<1.56 for p in pp):
            continue
        poly.material_index=slot
        for li in poly.loop_indices:
            p=positions[mesh.data.loops[li].vertex_index]
            u=center+max(-13,min(13,p.y*330))
            v=437+max(0,min(52,(abs(p.x)-wrist)*330))
            uv.data[li].uv=(u/1774,1-v/887)


def a_pose(rig):
    clear_pose(rig)
    for side,s in (("Left",1),("Right",-1)):
        bone=rig.pose.bones[side+"UpperArm"]
        rest=bone.bone.matrix_local.to_quaternion()
        bone.rotation_quaternion=rest.inverted()@Quaternion((0,1,0),s*math.radians(61))@rest


def studio():
    col=bpy.data.collections.new("Review studio")
    scene.collection.children.link(col)
    def light(name,location,energy,size):
        data=bpy.data.lights.new(name,"AREA");data.energy=energy;data.shape="DISK";data.size=size
        ob=bpy.data.objects.new(name,data);col.objects.link(ob);ob.location=location
        ob.rotation_euler=(Vector((0,0,1))-ob.location).to_track_quat("-Z","Y").to_euler()
    light("Soft key",(-3,-4,5),450,4)
    light("Fill",(4,-1,3),270,3)
    light("Rim",(1,3,4),450,3)
    bpy.ops.mesh.primitive_plane_add(size=200)
    floor=bpy.context.object;floor.name="Review floor";move_collection(floor,col)
    floor.data.materials.append(make_material(ROOT,"Review neutral grey",(.14,.15,.16),.9,0,"cloth"))
    data=bpy.data.cameras.new("Review camera");cam=bpy.data.objects.new("Review camera",data);col.objects.link(cam)
    data.type="ORTHO";scene.camera=cam
    if scene.world is None:
        scene.world=bpy.data.worlds.new("Reference review world")
    scene.world.use_nodes=True
    scene.world.node_tree.nodes["Background"].inputs[0].default_value=(.18,.18,.18,1)
    scene.world.node_tree.nodes["Background"].inputs[1].default_value=.45
    scene.render.engine="CYCLES";scene.cycles.device="CPU";scene.cycles.samples=12
    scene.cycles.use_denoising=True
    scene.render.resolution_percentage=100
    scene.render.image_settings.file_format="PNG"
    scene.view_settings.view_transform="AgX"
    return cam


def set_camera(cam,position,target,scale):
    cam.location=position
    cam.rotation_euler=(Vector(target)-cam.location).to_track_quat("-Z","Y").to_euler()
    cam.data.ortho_scale=scale


def add_reference_planes(kind):
    col=bpy.data.collections.new(kind+" | Reference sheet (toggle viewport)")
    scene.collection.children.link(col)
    image=bpy.data.images.load(str(REFS/(kind+".png")),check_existing=True);image.pack()
    ob=bpy.data.objects.new(kind+" four-view concept reference",None);col.objects.link(ob)
    ob.empty_display_type="IMAGE";ob.data=image;ob.empty_display_size=5
    ob.rotation_euler=(math.pi/2,0,0);ob.location=(0,.8,1)
    ob.color=(1,1,1,.75);ob.empty_image_depth="BACK"
    col.hide_viewport=True;col.hide_render=True


def main(kind):
    global scene,ASSETS,current,pieces,bones,orange,hair,undershirt,rope,stripe
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version=0
    scene=bpy.context.scene;scene.unit_settings.system="METRIC";scene.render.fps=30
    ASSETS=bpy.data.collections.new("Reference revision characters")
    scene.collection.children.link(ASSETS)
    current=None;pieces=[];bones={}
    random.seed(241020)
    orange=make_material(ROOT,"R3_Industrial_orange",(.30,.115,.035),.88,0,"cloth")
    hair=make_material(ROOT,"R3_Hair",(.022,.017,.012),.87,0,"cloth")
    undershirt=make_material(ROOT,"R3_Undershirt",(.042,.035,.028),.9,0,"cloth")
    rope=make_material(ROOT,"R3_Rope",(.14,.105,.068),.94,0,"cloth")
    stripe=make_material(ROOT,"R3_Stripe",(.31,.28,.22),.8,0,"cloth")
    exec(compile(prepare_source(),str(ROOT/"derived_original_builder"),"exec"),globals())
    print("BUILD_BEGIN",kind,flush=True)
    rig,mesh,col=build(kind)
    print("GEOMETRY_COMPLETE",kind,len(mesh.data.vertices),flush=True)
    clear_pose(rig)
    rig.location=(0,0,0)
    width={"Hunter":1.03,"Runner":.82,"Ambusher":.91,"Elite":1.30}[kind]
    # Smooth the old raised shoulder caps into a sloping shoulder silhouette.
    inverse=mesh.matrix_world.inverted()
    for vertex in mesh.data.vertices:
        p=mesh.matrix_world@vertex.co
        if 1.37<p.z<1.62:
            influence=math.exp(-((abs(p.x)-.205*width)/.075)**2)
            p.z-=.052*influence*max(0,min(1,(p.z-1.37)/.13))
            vertex.co=inverse@p
    mesh.data.update()
    # Source vertices were joined with the original pelvis object's transform.
    bpy.context.view_layer.update()
    saved_scale=rig.scale.copy()
    rig.scale=(1,1,1)
    bpy.context.view_layer.update()
    assign_reference_materials(mesh,kind,width)
    hand_material(mesh,kind,width)
    print("MATERIALS_COMPLETE",kind,flush=True)
    rig.scale=saved_scale
    bpy.context.view_layer.update()
    source_file=SOURCE/(kind+"_Animated.blend")
    with bpy.data.libraries.load(str(source_file),link=False) as (src,dst):
        dst.actions=[n for n in src.actions if n.startswith(kind+"_")]
        dst.armatures=[n for n in src.armatures if n.startswith(kind+"_")]
    original=dst.armatures[0]
    rest_errors=[]
    for bone in rig.data.bones:
        old=original.bones.get(bone.name)
        if old is None or max(abs(bone.matrix_local[r][c]-old.matrix_local[r][c]) for r in range(4) for c in range(4))>1e-6:
            rest_errors.append(bone.name)
    if rest_errors:
        raise RuntimeError("Skeleton changed: "+str(rest_errors))
    bpy.data.armatures.remove(original)
    actions=[a for a in dst.actions if a]
    for action in actions:action.use_fake_user=True
    rig["revision"]="ReferenceRevision3"
    rig["provenance"]="AI-assisted derivative of original project meshes; generated concept sheets used for shape and colour reference."
    rig["source_animated_blend"]=str(source_file)
    rig["review_status"]="Reference-directed modelling revision; final sculpt, retopology and seamless texture bake still require art review."
    cam=studio()
    add_reference_planes(kind)
    a_pose(rig)
    scene.render.resolution_x=850;scene.render.resolution_y=1000
    set_camera(cam,(2.8,-6,2.5),(0,0,1.03),2.32 if kind!="Elite" else 2.50)
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=="VIEW_3D":
                area.spaces.active.region_3d.view_perspective="CAMERA"
                area.spaces.active.shading.type="MATERIAL"
    bpy.ops.object.select_all(action="DESELECT");rig.select_set(True);bpy.context.view_layer.objects.active=rig
    scene.frame_start=1;scene.frame_end=90
    mesh.data.calc_loop_triangles()
    report=dict(character=kind,source=str(source_file),
                source_sha256=hashlib.sha256(source_file.read_bytes()).hexdigest(),
                vertices=len(mesh.data.vertices),triangles=len(mesh.data.loop_triangles),
                bones=len(rig.data.bones),rest_pose_matches_source=not rest_errors,
                unweighted_vertices=sum(not v.groups for v in mesh.data.vertices),
                actions=[a.name for a in actions],packed_images=sum(bool(i.packed_file) for i in bpy.data.images))
    out=ROOT/"Blender"/(kind+"_ReferenceRevision3.blend")
    bpy.ops.wm.save_as_mainfile(filepath=str(out))
    report["output_sha256"]=hashlib.sha256(out.read_bytes()).hexdigest()
    (ROOT/"Evidence"/(kind+".json")).write_text(json.dumps(report,indent=2))
    print("REVISION_SAVED",kind,json.dumps(report),flush=True)
    scene.render.filepath=str(ROOT/"Previews"/(kind+"_ThreeQuarter.png"))
    bpy.ops.render.render(write_still=True)
    print("REVISION_RENDERED",kind,flush=True)


if __name__=="__main__":
    args=sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else ["Hunter"]
    main(args[0])
