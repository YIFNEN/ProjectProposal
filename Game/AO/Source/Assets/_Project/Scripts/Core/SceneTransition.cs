using UnityEngine;
using UnityEngine.SceneManagement;

namespace AO.Core
{
    /// <summary>
    /// 4개 씬 (01_Title → 02_Lobby → GamePlayScene → 04_Result) 간 전환 헬퍼.
    /// 각 씬은 Build Settings에 등록되어야 함.
    /// </summary>
    public static class SceneTransition
    {
        public const string TitleScene    = "01_Title";
        public const string LobbyScene    = "02_Lobby";
        public const string GameplayScene = "GamePlayScene";
        public const string ResultScene   = "04_Result";

        public static void GoTo(string sceneName)
        {
            // 씬 전환 직전 EventBus 정리 — 씬에 살아남은 구독자가 다음 씬에서 잘못된 콜백 받지 않도록
            EventBus.ClearAllSubscribers();
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }

        public static void GoToTitle()    => GoTo(TitleScene);
        public static void GoToLobby()    => GoTo(LobbyScene);
        public static void GoToGameplay() => GoTo(GameplayScene);
        public static void GoToResult()   => GoTo(ResultScene);
    }
}
