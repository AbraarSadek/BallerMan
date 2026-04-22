using UnityEngine;
using UnityEngine.SceneManagement;

public class VRButton : MonoBehaviour
{
    public string endSceneName = "Level";
    public void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Hand"))
        {
            SceneManager.LoadScene(endSceneName);
        }
    }
}