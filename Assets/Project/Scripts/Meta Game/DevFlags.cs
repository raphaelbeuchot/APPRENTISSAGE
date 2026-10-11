// Interrupteurs de playtest, tous eteints par defaut. Seul le DevPanel les allume (editeur et builds de dev) :
// dans un build joueur, le DevPanel n'existe pas et ils restent toujours a false.
public static class DevFlags
{
    // PlayerHealth.TakeDamage ignore les degats (TakeHeadshot non concerne : elimination de la course multi)
    public static bool Invincible;

    // SentinelDetector ignore les joueurs (pas de tir, pas de silhouette, pas d'alarme)
    public static bool SentinelIgnoresPlayer;
}
