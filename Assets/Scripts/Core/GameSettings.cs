using UnityEngine;

[CreateAssetMenu(fileName = "GameSettings", menuName = "ProjectTowerRpg/GameSettings")]
public class GameSettings : ScriptableObject
{
    [Header("Nameplates")]
    public bool ShowPlayerNameplate = false;
    public bool ShowNpcNameplates = true;
    public bool ShowEnemyNameplates = true;
    public float MaxViewDistance = 50f; // 🆕 дистанция, дальше которой неймплейт скрывается
}
