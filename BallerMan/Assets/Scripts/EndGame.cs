using UnityEngine;
using TMPro;

public class EndGameDisplay : MonoBehaviour
{
    public TextMeshPro playerScoreText;
    public TextMeshPro agentScoreText;

    void Start()
    {
        playerScoreText.text = ScoreManager.Instance.playerScore.ToString();
        agentScoreText.text = ScoreManager.Instance.agentScore.ToString();
    }
}