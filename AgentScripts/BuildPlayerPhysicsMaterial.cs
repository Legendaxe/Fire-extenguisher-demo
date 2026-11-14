using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-time setup: gives the player capsule a frictionless physics material so it can slide
/// along walls instead of being pinned by static friction.
/// Run with: unity command script run --file AgentScripts/BuildPlayerPhysicsMaterial.cs --entry BuildPlayerPhysicsMaterial.Build
/// </summary>
public static class BuildPlayerPhysicsMaterial
{
    const string FolderParent = "Assets/Physics";
    const string FolderName = "Player";
    const string MaterialPath = FolderParent + "/" + FolderName + "/PM_Player.physicMaterial";
    const string PlayerPrefabPath = "Assets/Prefabs/Player/PF_Player.prefab";

    public static string Build()
    {
        var report = new List<string>();

        if (!AssetDatabase.IsValidFolder(FolderParent + "/" + FolderName))
            AssetDatabase.CreateFolder(FolderParent, FolderName);

        var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(MaterialPath);
        if (material == null)
        {
            material = new PhysicsMaterial("PM_Player");
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        material.dynamicFriction = 0f;
        material.staticFriction = 0f;
        material.bounciness = 0f;
        material.frictionCombine = PhysicsMaterialCombine.Minimum;
        material.bounceCombine = PhysicsMaterialCombine.Minimum;

        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();

        report.Add($"material={MaterialPath} friction={material.dynamicFriction}/{material.staticFriction} " +
                   $"combine={material.frictionCombine}");

        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            var capsule = root.GetComponent<CapsuleCollider>();
            if (capsule == null)
                throw new Exception("PF_Player has no CapsuleCollider.");

            capsule.sharedMaterial = material;

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath, out bool saved);
            if (!saved)
                throw new Exception($"Failed to save {PlayerPrefabPath}");

            report.Add($"capsuleMaterial={capsule.sharedMaterial.name}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return string.Join("\n", report);
    }
}
