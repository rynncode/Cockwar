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
    }

    private static Spec[] Specs()
    {
        return new Spec[]
        {
            // The classic arcing shell. Same numbers as your current projectile.
            new Spec { name = "Bazooka", blast = 15f, crater = 0f, damage = 50f, knockback = 60f,
                       minPower = 10f, maxPower = 70f },

            // Heavy arc, big hole, less damage, limited ammo.
            new Spec { name = "Mortar", blast = 20f, crater = 24f, damage = 40f, knockback = 70f,
                       scale = 1.1f, gravity = 1.6f, minPower = 20f, maxPower = 95f, ammo = 6,
                       colorLow = new Color(0.8f, 0.6f, 1f, 0.9f), colorFull = new Color(0.55f, 0.2f, 0.95f, 1f) },

            // Bounces and rolls, explodes when the 3 second fuse runs out.
            new Spec { name = "Grenade", blast = 18f, crater = 20f, damage = 55f, knockback = 70f,
                       scale = 0.8f, explodeOnImpact = false, fuse = 3f, bounciness = 0.45f, friction = 0.8f,
                       angularDamping = 0.2f, minPower = 10f, maxPower = 55f, ammo = 5,
                       colorLow = new Color(0.7f, 1f, 0.4f, 0.9f), colorFull = new Color(0.2f, 0.75f, 0.2f, 1f) },

            // One shot. Rolls, short fuse, huge blast.
            new Spec { name = "Holy Hand Grenade", blast = 32f, crater = 38f, damage = 100f, knockback = 120f,
                       scale = 1.3f, explodeOnImpact = false, fuse = 3f, bounciness = 0.35f, friction = 0.9f,
                       angularDamping = 0.3f, minPower = 10f, maxPower = 50f, ammo = 1,
                       colorLow = new Color(1f, 0.95f, 0.5f, 0.9f), colorFull = new Color(1f, 0.75f, 0.1f, 1f) },

            // Flat, fast, tiny blast, straight laser line. Fixed power.
            new Spec { name = "Sniper Rifle", blast = 4f, crater = 5f, damage = 75f, knockback = 30f,
                       scale = 0.35f, gravity = 0f, minPower = 160f, maxPower = 160f, ammo = 3,
                       style = AimStyle.Line, colorLow = new Color(1f, 0.15f, 0.15f, 0.9f),
                       lineWidth = 0.25f, lineMaxLength = 150f },

            // Digs a big hole, hurts nobody.
            new Spec { name = "Digger", blast = 6f, crater = 26f, damage = 0f, knockback = 0f,
                       scale = 0.9f, minPower = 10f, maxPower = 60f,
                       colorLow = new Color(0.85f, 0.65f, 0.4f, 0.9f), colorFull = new Color(0.55f, 0.35f, 0.15f, 1f) },
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
