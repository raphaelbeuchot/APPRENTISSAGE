using UnityEngine;
using System.Collections;

public class UIAudioPlayer : MonoBehaviour
{
    [Header("Audio Clips")]
    [SerializeField] private AudioClip soundUp;
    [SerializeField] private AudioClip soundDown;
    [SerializeField] private AudioClip soundLeft;
    [SerializeField] private AudioClip soundRight;
    [SerializeField] private AudioClip soundA;
    [SerializeField] private AudioClip soundB;

    [Header("Special Clips")]
    [SerializeField] private AudioClip soundNewGame;
    [SerializeField] private AudioClip soundLevelSelected;

    [Header("Config")]
    [SerializeField] private float volume = 1f;

    private AudioSource audioSource;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.ignoreListenerPause = true;
    }

    public void PlayUp() { Play(soundUp); }
    public void PlayDown() { Play(soundDown); }
    public void PlayLeft() { Play(soundLeft); }
    public void PlayRight() { Play(soundRight); }
    public void PlayA() { Play(soundA); }
    public void PlayB() { Play(soundB); }

    public void PlayNewGameAndSurvive()
    {
        Play(soundNewGame);
        DontDestroyOnLoad(gameObject);
        if (soundNewGame != null)
            StartCoroutine(SelfDestructAfter(soundNewGame.length));
    }
    public void PlayLevelSelectedAndSurvive()
    {
        Play(soundLevelSelected);
        DontDestroyOnLoad(gameObject);
        if (soundLevelSelected != null)
            StartCoroutine(SelfDestructAfter(soundLevelSelected.length));
    }

    IEnumerator SelfDestructAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        Destroy(gameObject);
    }

    void Play(AudioClip clip)
    {
        if (clip == null) return;
        audioSource.PlayOneShot(clip, volume);
    }
}