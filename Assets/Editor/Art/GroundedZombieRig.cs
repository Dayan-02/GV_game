using System.Linq;
using UnityEngine;
using UnityEditor;
public static class GroundedZombieRig
{
 public static void Build(){
 var source=AssetDatabase.LoadAssetAtPath<GameObject>(ImportedCharacterSetup.Zombie);
 var wrapper=new GameObject("CanonicalZombie");var go=Object.Instantiate(source,wrapper.transform);go.name="SourceRig";
 try{
 go.transform.localRotation=Quaternion.Euler(0,-90,0);
 var old=go.GetComponent<Animator>();
 foreach(bool left in new[]{true,false}){
 var thigh=old.GetBoneTransform(left?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg);
 var calf=old.GetBoneTransform(left?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg);
 var foot=old.GetBoneTransform(left?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);
 var toe=old.GetBoneTransform(left?HumanBodyBones.LeftToes:HumanBodyBones.RightToes);
 Aim(thigh,calf,-Vector3.up+Vector3.forward*.015f);Aim(calf,foot,-Vector3.up-Vector3.forward*.015f);if(toe!=null)Aim(foot,toe,Vector3.forward);
 Aim(old.GetBoneTransform(left?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm),old.GetBoneTransform(left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm),left?Vector3.left:Vector3.right);
 Aim(old.GetBoneTransform(left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm),old.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand),left?Vector3.left:Vector3.right);
 }
 var d=((ModelImporter)AssetImporter.GetAtPath(ImportedCharacterSetup.Zombie)).humanDescription;
 d.skeleton=wrapper.GetComponentsInChildren<Transform>().Select(t=>new SkeletonBone{name=t.name,position=t.localPosition,rotation=t.localRotation,scale=t.localScale}).ToArray();
 Object.DestroyImmediate(old);var a=wrapper.AddComponent<Animator>();var avatar=AvatarBuilder.BuildHumanAvatar(wrapper,d);avatar.name="CanonicalZombieAvatar";
 if(!avatar.isHuman||!avatar.isValid)throw new System.Exception("Invalid canonical avatar");
 string path="Assets/Animations/GroundedEnemies/CanonicalZombieAvatar.asset";var existing=AssetDatabase.LoadAssetAtPath<Avatar>(path);
 if(existing==null){AssetDatabase.CreateAsset(avatar,path);existing=avatar;}else{EditorUtility.CopySerialized(avatar,existing);Object.DestroyImmediate(avatar);}
 a.avatar=existing;a.runtimeAnimatorController=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/GroundedEnemies/Hunter.overrideController");a.applyRootMotion=false;a.cullingMode=AnimatorCullingMode.AlwaysAnimate;
 var material=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Hunter.prefab").GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterial;
 foreach(var skin in go.GetComponentsInChildren<SkinnedMeshRenderer>()){skin.sharedMaterial=material;skin.updateWhenOffscreen=true;}
 PrefabUtility.SaveAsPrefabAsset(wrapper,"Assets/Animations/GroundedEnemies/CanonicalZombie.prefab");AssetDatabase.SaveAssets();
 }finally{Object.DestroyImmediate(wrapper);}
 }
 private static void Aim(Transform bone,Transform child,Vector3 direction){bone.rotation=Quaternion.FromToRotation(child.position-bone.position,direction)*bone.rotation;}
}

