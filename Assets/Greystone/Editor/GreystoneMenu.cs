// =====================================================================================
//  GreystoneMenu.cs — Εργαλεία Editor (μενού «Greystone»)
//  ---------------------------------------------------------------------------------
//  * «Δημιουργία σκηνής χωριού»: φτιάχνει και αποθηκεύει τη σκηνή Greystone.unity με το
//    αντικείμενο GameBootstrap και την προσθέτει στα Build Settings.
//  * «Ρύθμιση project (Linear χρώμα)»: ορίζει Linear color space, που δίνει φυσικότερο
//    φωτισμό PBR.
//  Ο κόσμος χτίζεται κατά την εκτέλεση (Play), οπότε η σκηνή περιέχει μόνο τον bootstrap.
// =====================================================================================
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Greystone.EditorTools
{
    public static class GreystoneMenu
    {
        const string ScenePath = "Assets/Greystone/Scenes/Greystone.unity";

        [MenuItem("Greystone/Δημιουργία σκηνής χωριού")]
        static void CreateScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Greystone").AddComponent<GameBootstrap>();
            System.IO.Directory.CreateDirectory("Assets/Greystone/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.Refresh();
            Debug.Log("[Greystone] Η σκηνή δημιουργήθηκε: " + ScenePath + " — πατήστε Play.");
        }

        [MenuItem("Greystone/Ρύθμιση project (Linear χρώμα)")]
        static void ConfigureProject()
        {
            PlayerSettings.colorSpace = ColorSpace.Linear;
            Debug.Log("[Greystone] Ορίστηκε Linear color space.");
        }
    }
}
#endif
