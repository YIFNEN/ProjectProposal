using UnityEngine;
using UnityEngine.SceneManagement;

namespace AO.UI
{
    public class MenuSceneWorldBootstrap : MonoBehaviour
    {
        [SerializeField] private MenuSceneCharacterMode _characterMode = MenuSceneCharacterMode.None;

        private void Awake()
        {
            MenuSceneWorld.EnsureCommonBackdrop(ResolveCharacterMode());
        }

        private void OnEnable()
        {
            MenuSceneWorld.EnsureCommonBackdrop(ResolveCharacterMode());
        }

        private MenuSceneCharacterMode ResolveCharacterMode()
        {
            if (_characterMode != MenuSceneCharacterMode.None) return _characterMode;

            return SceneManager.GetActiveScene().name == "02_Lobby"
                ? MenuSceneCharacterMode.LobbyLeft
                : MenuSceneCharacterMode.None;
        }
    }
}
