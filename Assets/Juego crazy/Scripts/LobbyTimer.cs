using UnityEngine;
using TMPro;
using Mirror;

public class LobbyTimer : NetworkBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text timerText;

    [Header("Settings")]
    [SerializeField] private float countdownTime = 5f;

    [SyncVar(hook = nameof(OnTimerChanged))]
    private float currentTimer;

    [SyncVar]
    private bool isCountingDown = false;

    private void Update()
    {
        if (!isServer) return;

        NetworkRoomManager room = NetworkManager.singleton as NetworkRoomManager;
        if (room != null)
        {
            bool allReady = room.roomSlots.Count >= 2 && room.allPlayersReady;

            if (allReady)
            {
                if (!isCountingDown)
                {
                    isCountingDown = true;
                    currentTimer = countdownTime;
                }

                currentTimer -= Time.deltaTime;
                if (currentTimer <= 0f)
                {
                    currentTimer = 0f;
                }
            }
            else
            {
                isCountingDown = false;
                currentTimer = countdownTime;
            }
        }
    }

    private void OnTimerChanged(float oldTime, float newTime)
    {
        if (timerText == null) return;

        if (isCountingDown)
        {
            timerText.text = $"El juego inicia en: {Mathf.CeilToInt(newTime)}s";
        }
        else
        {
            timerText.text = "Esperando que ambos jugadores presionen Ready...";
        }
    }
}
