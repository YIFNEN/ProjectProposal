using UnityEngine;
using UnityEngine.SceneManagement;

namespace AO.UI
{
    public enum MenuSceneCharacterMode
    {
        None,
        LobbyLeft,
        ResultLeft,
    }

    public static class MenuSceneWorld
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitializeSceneLoadedHook()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            ApplyForScene(SceneManager.GetActiveScene().name);
        }

        public static void EnsureCommonBackdrop(MenuSceneCharacterMode characterMode)
        {
            PreserveSceneLighting();
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ApplyForScene(scene.name);
        }

        private static void ApplyForScene(string sceneName)
        {
            switch (sceneName)
            {
                case "01_Title":
                    EnsureCommonBackdrop(MenuSceneCharacterMode.None);
                    break;
                case "02_Lobby":
                    EnsureCommonBackdrop(MenuSceneCharacterMode.LobbyLeft);
                    break;
                case "04_Result":
                    EnsureCommonBackdrop(MenuSceneCharacterMode.ResultLeft);
                    break;
            }
        }

        private static void PreserveSceneLighting()
        {
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (light.type != LightType.Directional) continue;
                if (light.name != "Directional Light" && RenderSettings.sun != null) continue;

                RenderSettings.sun = light;
                if (light.name == "Directional Light") break;
            }
        }
    }
}
