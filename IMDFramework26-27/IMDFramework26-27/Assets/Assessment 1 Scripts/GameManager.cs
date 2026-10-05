using UnityEngine;
public class GameManager : MonoBehaviour
{
    [SerializeField] private GameObject m_PlayerPrefab;
    [SerializeField] private Transform m_PlayerSpawnPoint;
    private GameObject m_PlayerRef;
    private void Start()
    {
        SpawnPlayer();
    }
    private void SpawnPlayer()
    {
        m_PlayerRef = Instantiate(
            m_PlayerPrefab,
            m_PlayerSpawnPoint.position,
            m_PlayerSpawnPoint.rotation
        );
        PlayerManager playerManager =
            m_PlayerRef.GetComponent<PlayerManager>();
        playerManager.Init();
    }
}