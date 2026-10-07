using UnityEngine;
using Mirror;

[RequireComponent(typeof(AudioSource))]
public class Musica_manager : NetworkBehaviour
{
    private AudioSource audioSource;

    // Guarda en la red el momento exacto (NetworkTime) en que comenzó la música
    [SyncVar(hook = nameof(OnMusicStarted))]
    private double musicStartTime;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = true;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        // El servidor fija el tiempo de arranque de la pista
        musicStartTime = NetworkTime.time;
    }

    // Hook: se ejecuta cuando un cliente recibe el tiempo de inicio
    private void OnMusicStarted(double oldTime, double newTime)
    {
        SyncAndPlay();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (musicStartTime > 0)
        {
            SyncAndPlay();
        }
    }

    private void SyncAndPlay()
    {
        if (audioSource.clip == null) return;

        // Calcular cuántos segundos lleva sonando la pista en el servidor
        double elapsedTime = NetworkTime.time - musicStartTime;
        float songLength = audioSource.clip.length;

        // Si la canción está loopeada, calculamos la posición exacta con módulo (%)
        float playbackPosition = (float)(elapsedTime % songLength);

        audioSource.time = playbackPosition;
        if (!audioSource.isPlaying)
        {
            audioSource.Play();
        }
    }
}