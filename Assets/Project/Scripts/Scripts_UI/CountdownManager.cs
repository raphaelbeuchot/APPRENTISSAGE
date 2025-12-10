using UnityEngine;
using System.Collections;



public class CountdownManager : MonoBehaviour
{
    public MetalShutter shutter;
    public GameManager gameManager;
    [HideInInspector]
    public bool countdownFinished = false;

    public void StartCountdown()
    {
        GameUIManager uiManager = FindObjectOfType<GameUIManager>();
        if (uiManager != null)
        {
            uiManager.ShowCountdown();
        }
        
        Debug.Log("Countdown demarre!");
        StartCoroutine(CountdownCoroutine());
    }

    IEnumerator CountdownCoroutine()
    {
        Debug.Log("3...");
        yield return new WaitForSeconds(1f);

        Debug.Log("2...");
        yield return new WaitForSeconds(1f);

        Debug.Log("1...");
        yield return new WaitForSeconds(1f);

        Debug.Log("GO!");

        // Ouvrir le rideau
        if (shutter != null)
        {
            shutter.Open();
        }

        countdownFinished = true;

        // Demarrer le jeu
        if (gameManager != null)
        {
            gameManager.StartGameCycle();
        }
    }
}