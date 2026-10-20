"""Re-import exported files in a clean Blender process and inspect actual bytes."""
import bpy, json, math
from pathlib import Path
from mathutils import Vector, Quaternion

root=Path(__file__).resolve().parent
report=[]
for kind in ("Hunter","Runner","Ambusher","Elite"):
    for extension in ("glb","fbx"):
        for obj in list(bpy.data.objects): bpy.data.objects.remove(obj,do_unlink=True)
        path=root/"Exports"/(kind+"."+extension)
        if extension=="glb": bpy.ops.import_scene.gltf(filepath=str(path))
        else: bpy.ops.import_scene.fbx(filepath=str(path))
        meshes=[o for o in bpy.context.scene.objects if o.type=='MESH' and any(m.type=='ARMATURE' for m in o.modifiers)]
        rigs=[o for o in bpy.context.scene.objects if o.type=='ARMATURE']
        assert len(meshes)==1 and len(rigs)==1, (kind,extension,"Expected one skin and one rig",[(o.name,o.type) for o in bpy.context.scene.objects])
        mesh,rig=meshes[0],rigs[0]
        assert len(rig.data.bones)==52
        assert len(mesh.data.uv_layers)>0
        assert all(v.groups and abs(sum(g.weight for g in v.groups)-1)<.02 for v in mesh.data.vertices)
        assert any(m.type=='ARMATURE' and m.object==rig for m in mesh.modifiers)
        for name in ("Hips","Spine","Chest","Head","LeftUpperLeg","RightLowerLeg","LeftHand","RightFoot"):
            assert name in rig.data.bones, (kind,extension,name)
        vertices=[mesh.matrix_world@v.co for v in mesh.data.vertices]
        height=max(v.z for v in vertices)-min(v.z for v in vertices)
        assert 1.7<height<2.3, (kind,extension,height)
        missing=[]
        for material in mesh.data.materials:
            for node in material.node_tree.nodes:
                if node.type=='TEX_IMAGE' and (node.image is None or node.image.size[0]==0):
                    missing.append(material.name)
        assert not missing, missing
        # Static joint bends check skin integrity, not animation quality or Unity mapping.
        for name,angle in (("LeftUpperLeg",-25),("LeftLowerLeg",55),("RightUpperArm",40),("RightLowerArm",45)):
            p=rig.pose.bones[name];q=p.bone.matrix_local.to_quaternion()
            p.rotation_mode='QUATERNION'
            p.rotation_quaternion=q.inverted()@Quaternion(Vector((1,0,0)),math.radians(angle))@q
        bpy.context.view_layer.update()
        evaluated=mesh.evaluated_get(bpy.context.evaluated_depsgraph_get())
        deformed=evaluated.to_mesh()
        assert all(math.isfinite(c) for v in deformed.vertices for c in v.co)
        positions=[evaluated.matrix_world@v.co for v in deformed.vertices]
        largest=max(max(v[i] for v in positions)-min(v[i] for v in positions) for i in range(3))
        assert largest<3.5, (kind,extension,"Exploding joint deformation",largest)
        evaluated.to_mesh_clear()
        mesh.data.calc_loop_triangles()
        report.append(dict(model=kind,format=extension,imported=True,bones=len(rig.data.bones),
            triangles=len(mesh.data.loop_triangles),height_m=round(height,3),
            normalized_skin_weights=True,uvs=True,texture_images_present=True,
            static_bend_finite=True,unity_import_tested=False,animation_quality_tested=False))
(root/"Evidence"/"export_validation.json").write_text(json.dumps(report,indent=2))
print("EXPORT_VALIDATION_PASS",json.dumps(report))
