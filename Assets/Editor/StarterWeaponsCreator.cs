using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor tool. Put this file in a folder named "Editor" (Assets/Editor/).
/// Menu: Cockwar > Create Starter Weapons.
/// Builds ready-to-use weapons in Assets/Weapons: for each one a WeaponData asset, a projectile prefab
/// and an explosion prefab (copied from your existing Projectile and Explosion prefabs), plus a physics
/// material for the rolling grenades. Then you just drag the WeaponData assets into the weapon slots.
/// A weapon that already exists is skipped, so your own tuning is never overwritten.
/// To regenerate one, delete its asset (and its prefabs) and run the menu again.
/// </summary>
public static class StarterWeaponsCreator
{
    private const string Root = "Assets/Weapons";
    private const string PrefabFolder = Root + "/Prefabs";
    private const string MaterialFolder = Root + "/Materials";

    private class Spec
    {
        public string name;

        // Explosion (world units; your map is about 500 x 220)
        public float blast, crater, damage, knockback;

        // Projectile
        public float scale = 1f;
        public float gravity = 1f;
        public float mass = 1f;
        public bool explodeOnImpact = true;
        public float fuse = 3f;
        public bool fuseFromFirstHit = false;
        public float bounciness = -1f;      // < 0 = no physics material (plain impact shell)
        public float friction = 0.6f;
        public float angularDamping = 0.05f;

        // Weapon
        public float minPower, maxPower;
        public int ammo = -1;
        public AimStyle style = AimStyle.ChargeCone;
        public Color colorLow = new Color(1f, 0.85f, 0.2f, 0.9f);
        public Color colorFull = new Color(1f, 0.35f, 0.05f, 1f);
        public float lineWidth = 0.25f;
        public float lineMaxLength = 150f;

        // Multi-Shot
        public int multiShotCount = 1;
        public float multiShotSpread = 12f;
    }

    private static Spec[] Specs()
    {
        return new Spec[]
        {
            // Simple direct shot. The baseline every other weapon is compared to.
            new Spec { name = "Basic Projectile", blast = 15f, crater = 0f, damage = 50f, knockback = 60f,
                       minPower = 10f, maxPower = 70f },

            // Same projectile and explosion as Basic Projectile — the only
            // difference is a higher gravity scale, so it arcs shorter and
            // steeper instead of flying flat and far.
            new Spec { name = "Grenade", blast = 15f, crater = 0f, damage = 50f, knockback = 60f,
                       gravity = 1.6f, minPower = 10f, maxPower = 55f,
                       colorLow = new Color(0.7f, 1f, 0.4f, 0.9f), colorFull = new Color(0.2f, 0.75f, 0.2f, 1f) },

            // Same behaviour as Basic Projectile (explodes on impact, normal
            // arc) — just bigger, heavier and hits much harder.
            new Spec { name = "Heavy Projectile", blast = 24f, crater = 0f, damage = 85f, knockback = 95f,
                       scale = 1.6f, mass = 2.5f, minPower = 10f, maxPower = 55f,
                       colorLow = new Color(0.8f, 0.6f, 1f, 0.9f), colorFull = new Color(0.55f, 0.2f, 0.95f, 1f) },

            // One click fires several small shells in a fan. Each one does
            // less damage than Basic Projectile, so landing all of them is
            // what makes it strong rather than any single hit.
            new Spec { name = "Multi-Shot", blast = 10f, crater = 0f, damage = 25f, knockback = 35f,
                       scale = 0.7f, minPower = 10f, maxPower = 70f,
                       multiShotCount = 3, multiShotSpread = 14f,
                       colorLow = new Color(0.5f, 0.9f, 1f, 0.9f), colorFull = new Color(0.1f, 0.6f, 0.9f, 1f) },

            // Same projectile and explosion as Basic Projectile, but it
            // ignores impacts and explodes on its own after a timer instead.
            new Spec { name = "Timed Projectile", blast = 15f, crater = 0f, damage = 50f, knockback = 60f,
                       explodeOnImpact = false, fuse = 3f, minPower = 10f, maxPower = 70f,
                       colorLow = new Color(1f, 0.6f, 0.3f, 0.9f), colorFull = new Color(1f, 0.25f, 0.05f, 1f) },
        };
    }

    [MenuItem("Cockwar/Create Starter Weapons")]
    public static void CreateAll()
    {
        string projectileSource = FindPrefab("Projectile");
        string explosionSource = FindPrefab("Explosion");
        if (projectileSource == null || explosionSource == null)
        {
            EditorUtility.DisplayDialog("Starter Weapons",
                "Could not find prefabs named exactly 'Projectile' and 'Explosion'. They are used as templates.", "OK");
            return;
        }

        EnsureFolder(Root);
        EnsureFolder(PrefabFolder);
        EnsureFolder(MaterialFolder);

        StringBuilder created = new StringBuilder();
        StringBuilder skipped = new StringBuilder();
        WeaponData first = null;

        foreach (Spec spec in Specs())
        {
            string weaponPath = Root + "/" + spec.name + ".asset";
            if (AssetDatabase.LoadAssetAtPath<WeaponData>(weaponPath) != null)
            {
                skipped.AppendLine("  " + spec.name);
                continue;
            }

            string explosionPath = PrefabFolder + "/" + spec.name + " Explosion.prefab";
            string projectilePath = PrefabFolder + "/" + spec.name + " Projectile.prefab";

            BuildExplosion(explosionSource, explosionPath, spec);
            BuildProjectile(projectileSource, projectilePath, explosionPath, spec);

            WeaponData data = ScriptableObject.CreateInstance<WeaponData>();
            data.displayName = spec.name;
            data.projectilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(projectilePath);
            data.minPower = spec.minPower;
            data.maxPower = spec.maxPower;
            data.ammo = spec.ammo;
            data.endsTurnOnFire = true;
            data.aimStyle = spec.style;
            data.aimColor = spec.colorLow;
            data.aimColorFull = spec.colorFull;
            data.lineWidth = spec.lineWidth;
            data.lineMaxLength = spec.lineMaxLength;
            data.multiShotCount = spec.multiShotCount;
            data.multiShotSpreadDegrees = spec.multiShotSpread;
            AssetDatabase.CreateAsset(data, weaponPath);

            if (first == null) first = data;
            created.AppendLine("  " + spec.name);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (first != null)
        {
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = first;
        }

        string message = "Created:\n" + (created.Length > 0 ? created.ToString() : "  (nothing new)\n");
        if (skipped.Length > 0) message += "\nAlready existed, left alone:\n" + skipped;
        message += "\nWeapons are in " + Root + ". Drag them into the Weapons list on each cockroach.";
        EditorUtility.DisplayDialog("Starter Weapons", message, "OK");
    }

    private static void BuildExplosion(string sourcePath, string destPath, Spec spec)
    {
        AssetDatabase.CopyAsset(sourcePath, destPath);
        GameObject root = PrefabUtility.LoadPrefabContents(destPath);
        try
        {
            Explosion explosion = root.GetComponentInChildren<Explosion>();
            if (explosion != null)
            {
                float baseBlast = Mathf.Max(0.01f, explosion.blastRadius);
                explosion.blastRadius = spec.blast;
                explosion.craterRadius = spec.crater;
                explosion.maxDamage = Mathf.RoundToInt(spec.damage);
                explosion.maxKnockback = spec.knockback;

                // Scale the explosion graphic with the blast so a big bomb also looks big.
                float visualScale = Mathf.Clamp(spec.blast / baseBlast, 0.3f, 4f);
                root.transform.localScale *= visualScale;
            }
            PrefabUtility.SaveAsPrefabAsset(root, destPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void BuildProjectile(string sourcePath, string destPath, string explosionPath, Spec spec)
    {
        AssetDatabase.CopyAsset(sourcePath, destPath);
        GameObject root = PrefabUtility.LoadPrefabContents(destPath);
        try
        {
            Projectile projectile = root.GetComponent<Projectile>();
            if (projectile != null)
            {
                projectile.explosionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(explosionPath);
                projectile.explodeOnImpact = spec.explodeOnImpact;
                projectile.fuseSeconds = spec.fuse;
                projectile.fuseStartsOnFirstHit = spec.fuseFromFirstHit;
            }

            Rigidbody2D body = root.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.gravityScale = spec.gravity;
                body.mass = spec.mass;
                body.angularDamping = spec.angularDamping;
                // Fast or small projectiles must not tunnel through the ground.
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            }

            root.transform.localScale *= spec.scale;

            if (spec.bounciness >= 0f)
            {
                Collider2D col = root.GetComponent<Collider2D>();
                if (col != null) col.sharedMaterial = GetOrCreatePhysicsMaterial(spec);
            }

            PrefabUtility.SaveAsPrefabAsset(root, destPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static PhysicsMaterial2D GetOrCreatePhysicsMaterial(Spec spec)
    {
        string path = MaterialFolder + "/" + spec.name + " Bounce.physicsMaterial2D";
        PhysicsMaterial2D existing = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(path);
        if (existing != null) return existing;

        PhysicsMaterial2D mat = new PhysicsMaterial2D(spec.name + " Bounce");
        mat.bounciness = spec.bounciness;
        mat.friction = spec.friction;
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static string FindPrefab(string exactName)
    {
        foreach (string guid in AssetDatabase.FindAssets(exactName + " t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) == exactName) return path;
        }
        return null;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = Path.GetFileName(path);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
