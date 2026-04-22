using UnityEngine;
using TMPro;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance;

    public int playerScore = 0;
    public int agentScore = 0;

    public TextMeshPro playerScoreText;
    public TextMeshPro agentScoreText;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        UpdateBoard();
    }

    public void AddPlayerPoint()
    {
        playerScore++;
        UpdateBoard();
    }

    public void AddAgentPoint()
    {
        agentScore++;
        UpdateBoard();
    }

    public void UpdateBoard()
    {
        if (playerScoreText != null)
            playerScoreText.text = playerScore.ToString();

        if (agentScoreText != null)
            agentScoreText.text = agentScore.ToString();
    }

    public void ResetScores()
    {
        playerScore = 0;
        agentScore = 0;
        UpdateBoard();
    }
}