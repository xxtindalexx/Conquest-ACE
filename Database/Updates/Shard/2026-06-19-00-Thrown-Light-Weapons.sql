/* =====================================================================
   Thrown Light Weapons (melee pipeline)

   Tags selected Light Weapon weenies so they can be thrown with infinite
   use while being computed on the MELEE damage pipeline (LW skill, melee
   augmentations, melee defense), routed through the missile launch path.

   Server code support (already implemented):
     - PropertyBool.IsMeleeThrownWeapon (9050)
     - Creature_Missile.LaunchProjectile() tags the projectile + sets launcher
     - DamageEvent: forces CombatType=Melee, melee aug pool
     - Player_Combat.GetTargetEffectiveDefenseSkill(): melee defense

   Property set applied per weenie:
     int    1   ItemType            = 256       (MissileWeapon)
     int    9   ValidLocations      = 4194304   (MissileWeapon slot)
     int   46   DefaultCombatStyle  = 128       (ThrownWeapon)
     int   48   WeaponSkill         = 45        (LightWeapons)
     int   51   CombatUse           = 2         (Missile)
     bool  63   UnlimitedUse        = 1         (infinite throwing, never consumed)
     bool 9050  IsMeleeThrownWeapon = 1         (forces melee damage pipeline)

   Optional balance/tuning (left to the weapon designer, see block at bottom):
     int   44   Damage
     int   49   WeaponTime
     float 22   DamageVariance
     float 26   MaximumVelocity   (projectile speed / range)

   IMPORTANT:
     - Fill in the real WCIDs in tmp_thrown_lw_wcids below.
     - The chosen weenies MUST already have a Setup/MotionTable/CombatTable that
       supports thrown-weapon animations (model them off an existing in-game
       thrown weapon, e.g. a throwing dagger/axe). This script only applies the
       tagging properties; it does not create art/motion data.
     - Requires the world database to be schema-named `ace_world`.
   ===================================================================== */

/* ---------------------------------------------------------------------
   0) Target WCIDs - EDIT THIS LIST
   --------------------------------------------------------------------- */
DROP TEMPORARY TABLE IF EXISTS tmp_thrown_lw_wcids;
CREATE TEMPORARY TABLE tmp_thrown_lw_wcids (wcid INT UNSIGNED PRIMARY KEY);
INSERT INTO tmp_thrown_lw_wcids (wcid) VALUES
    /* TODO: replace with the real thrown light weapon WCIDs, e.g. (700001),(700002) */
    (0);

/* ---------------------------------------------------------------------
   1) World weenie definitions (ace_world)
   --------------------------------------------------------------------- */
INSERT INTO ace_world.weenie_properties_int (object_Id, `type`, value)
SELECT wcid, 1,    256     FROM tmp_thrown_lw_wcids ON DUPLICATE KEY UPDATE value = VALUES(value);
INSERT INTO ace_world.weenie_properties_int (object_Id, `type`, value)
SELECT wcid, 9,    4194304 FROM tmp_thrown_lw_wcids ON DUPLICATE KEY UPDATE value = VALUES(value);
INSERT INTO ace_world.weenie_properties_int (object_Id, `type`, value)
SELECT wcid, 46,   128     FROM tmp_thrown_lw_wcids ON DUPLICATE KEY UPDATE value = VALUES(value);
INSERT INTO ace_world.weenie_properties_int (object_Id, `type`, value)
SELECT wcid, 48,   45      FROM tmp_thrown_lw_wcids ON DUPLICATE KEY UPDATE value = VALUES(value);
INSERT INTO ace_world.weenie_properties_int (object_Id, `type`, value)
SELECT wcid, 51,   2       FROM tmp_thrown_lw_wcids ON DUPLICATE KEY UPDATE value = VALUES(value);

INSERT INTO ace_world.weenie_properties_bool (object_Id, `type`, value)
SELECT wcid, 63,   1       FROM tmp_thrown_lw_wcids ON DUPLICATE KEY UPDATE value = VALUES(value);
INSERT INTO ace_world.weenie_properties_bool (object_Id, `type`, value)
SELECT wcid, 9050, 1       FROM tmp_thrown_lw_wcids ON DUPLICATE KEY UPDATE value = VALUES(value);

/* ---------------------------------------------------------------------
   2) Propagate to existing live biotas on the shard
   --------------------------------------------------------------------- */
INSERT INTO biota_properties_int (object_Id, `type`, value)
SELECT b.id, 1,    256     FROM biota b JOIN tmp_thrown_lw_wcids t ON b.weenie_Class_Id = t.wcid
ON DUPLICATE KEY UPDATE value = VALUES(value);
INSERT INTO biota_properties_int (object_Id, `type`, value)
SELECT b.id, 9,    4194304 FROM biota b JOIN tmp_thrown_lw_wcids t ON b.weenie_Class_Id = t.wcid
ON DUPLICATE KEY UPDATE value = VALUES(value);
INSERT INTO biota_properties_int (object_Id, `type`, value)
SELECT b.id, 46,   128     FROM biota b JOIN tmp_thrown_lw_wcids t ON b.weenie_Class_Id = t.wcid
ON DUPLICATE KEY UPDATE value = VALUES(value);
INSERT INTO biota_properties_int (object_Id, `type`, value)
SELECT b.id, 48,   45      FROM biota b JOIN tmp_thrown_lw_wcids t ON b.weenie_Class_Id = t.wcid
ON DUPLICATE KEY UPDATE value = VALUES(value);
INSERT INTO biota_properties_int (object_Id, `type`, value)
SELECT b.id, 51,   2       FROM biota b JOIN tmp_thrown_lw_wcids t ON b.weenie_Class_Id = t.wcid
ON DUPLICATE KEY UPDATE value = VALUES(value);

INSERT INTO biota_properties_bool (object_Id, `type`, value)
SELECT b.id, 63,   1       FROM biota b JOIN tmp_thrown_lw_wcids t ON b.weenie_Class_Id = t.wcid
ON DUPLICATE KEY UPDATE value = VALUES(value);
INSERT INTO biota_properties_bool (object_Id, `type`, value)
SELECT b.id, 9050, 1       FROM biota b JOIN tmp_thrown_lw_wcids t ON b.weenie_Class_Id = t.wcid
ON DUPLICATE KEY UPDATE value = VALUES(value);

/* ---------------------------------------------------------------------
   3) OPTIONAL balance/tuning - uncomment and set values as desired.
      Apply to BOTH the world weenie and the live biotas.
   ---------------------------------------------------------------------
   -- Example: Damage=20 (int 44), WeaponTime=40 (int 49),
   --          DamageVariance=0.25 (float 22), MaximumVelocity=20 (float 26)

   -- INSERT INTO ace_world.weenie_properties_int (object_Id, `type`, value)
   -- SELECT wcid, 44, 20 FROM tmp_thrown_lw_wcids ON DUPLICATE KEY UPDATE value = VALUES(value);
   -- INSERT INTO ace_world.weenie_properties_int (object_Id, `type`, value)
   -- SELECT wcid, 49, 40 FROM tmp_thrown_lw_wcids ON DUPLICATE KEY UPDATE value = VALUES(value);
   -- INSERT INTO ace_world.weenie_properties_float (object_Id, `type`, value)
   -- SELECT wcid, 22, 0.25 FROM tmp_thrown_lw_wcids ON DUPLICATE KEY UPDATE value = VALUES(value);
   -- INSERT INTO ace_world.weenie_properties_float (object_Id, `type`, value)
   -- SELECT wcid, 26, 20 FROM tmp_thrown_lw_wcids ON DUPLICATE KEY UPDATE value = VALUES(value);

   -- INSERT INTO biota_properties_int (object_Id, `type`, value)
   -- SELECT b.id, 44, 20 FROM biota b JOIN tmp_thrown_lw_wcids t ON b.weenie_Class_Id = t.wcid
   -- ON DUPLICATE KEY UPDATE value = VALUES(value);
   -- INSERT INTO biota_properties_int (object_Id, `type`, value)
   -- SELECT b.id, 49, 40 FROM biota b JOIN tmp_thrown_lw_wcids t ON b.weenie_Class_Id = t.wcid
   -- ON DUPLICATE KEY UPDATE value = VALUES(value);
   -- INSERT INTO biota_properties_float (object_Id, `type`, value)
   -- SELECT b.id, 22, 0.25 FROM biota b JOIN tmp_thrown_lw_wcids t ON b.weenie_Class_Id = t.wcid
   -- ON DUPLICATE KEY UPDATE value = VALUES(value);
   -- INSERT INTO biota_properties_float (object_Id, `type`, value)
   -- SELECT b.id, 26, 20 FROM biota b JOIN tmp_thrown_lw_wcids t ON b.weenie_Class_Id = t.wcid
   -- ON DUPLICATE KEY UPDATE value = VALUES(value);
   --------------------------------------------------------------------- */

DROP TEMPORARY TABLE IF EXISTS tmp_thrown_lw_wcids;
