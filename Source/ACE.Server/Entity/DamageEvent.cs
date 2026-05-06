using ACE.Common;
using ACE.DatLoader.Entity.AnimationHooks;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ACE.Server.Entity
{
    public class DamageEvent
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private const float DefaultSplitArrowDamageMultiplier = 0.6f;

        // factors:
        // - lifestone protection
        // - evade
        //   - offense mod (heart seeker)
        //      - accuracy mod (missile)
        //   - defense mod (defender)
        //      - stamina mod
        // - base damage / mod
        // - damage rating / mod
        //   - recklessness
        //   - sneak attack
        //   - heritage bonus
        // - damage resistance rating /mod
        // - power meter mod
        // - critical (chance % mod / critical damage mod)
        // - attribute mod
        // - armor / mod (base al, impen / bane, life armor / imperil)
        // - elemental damage bonus
        // - slayer mod
        // - resistance mod (natural, prot, vuln)
        //   - resistance cleaving
        // - shield mod
        // - rending mod

        public Creature Attacker;
        public Creature Defender;

        public CombatType CombatType;   // melee / missile / magic

        public WorldObject DamageSource;
        public DamageType DamageType;

        public WorldObject Weapon;      // the attacker's weapon. this can be different from DamageSource,
                                        // ie. for a missile attack, the missile would the DamageSource,
                                        // and the buffs would come from the Weapon

        public AttackType AttackType;   // slash / thrust / punch / kick / offhand / multistrike
        public AttackHeight AttackHeight;

        public bool LifestoneProtection;

        public float EvasionChance;
        public uint EffectiveAttackSkill;
        public uint EffectiveDefenseSkill;
        public float AccuracyMod;

        public bool Evaded;

        public BaseDamageMod BaseDamageMod;
        public float BaseDamage { get; set; }

        public float AttributeMod;
        public float PowerMod;
        public float SlayerMod;

        public float DamageRatingBaseMod;
        public float RecklessnessMod;
        public float SneakAttackMod;
        public float HeritageMod;
        public float PkDamageMod;

        public float DamageRatingMod;

        public bool IsCritical;

        public float CriticalChance;
        public float CriticalDamageMod;

        public float CriticalDamageRatingMod;
        public float CriticalDamageResistanceRatingMod;

        public float DamageBeforeMitigation;

        public float ArmorMod;
        public float EffectiveAL;        // Total effective armor level before conversion to ArmorMod
        public int BodyArmorMod;         // Life magic armor buff/debuff (Armor Self / Imperil)
        public float ResistanceMod;
        public float ShieldMod;
        public float WeaponResistanceMod;

        public float DamageResistanceRatingBaseMod;
        public float DamageResistanceRatingMod;
        public float PkDamageResistanceMod;

        public float DamageMitigated;

        // creature attacker
        public MotionCommand? AttackMotion;
        public AttackHook AttackHook;
        public KeyValuePair<CombatBodyPart, PropertiesBodyPart> AttackPart;      // the body part this monster is attacking with

        // creature defender
        public Quadrant Quadrant;

        public bool IgnoreMagicArmor =>  (Weapon?.IgnoreMagicArmor ?? false) || (Attacker?.IgnoreMagicArmor ?? false);      // ignores impen / banes

        public bool IgnoreMagicResist => (Weapon?.IgnoreMagicResist ?? false) || (Attacker?.IgnoreMagicResist ?? false);    // ignores life armor / prots

        public bool Overpower;


        // player defender
        public BodyPart BodyPart;
        public List<WorldObject> Armor;

        // creature defender
        public KeyValuePair<CombatBodyPart, PropertiesBodyPart> PropertiesBodyPart;
        public Creature_BodyPart CreaturePart;

        public float Damage;

        public bool GeneralFailure;

        public bool HasDamage => !Evaded && !LifestoneProtection;

        public bool CriticalDefended;

        public static HashSet<uint> AllowDamageTypeUndef = new HashSet<uint>()
        {
            22545,  // Obsidian Spines
            35191,  // Thunder Chicken
            38406,  // Blessed Moar
            38587,  // Ardent Moar
            38588,  // Blessed Moar
            38586,  // Verdant Moar
            40298,  // Ardent Moar
            40300,  // Blessed Moar
            40301,  // Verdant Moar
        };

        public static DamageEvent CalculateDamage(Creature attacker, Creature defender, WorldObject damageSource, MotionCommand? attackMotion = null, AttackHook attackHook = null)
        {
            var damageEvent = new DamageEvent();
            damageEvent.AttackMotion = attackMotion;
            damageEvent.AttackHook = attackHook;
            if (damageSource == null)
                damageSource = attacker;

            var damage = damageEvent.DoCalculateDamage(attacker, defender, damageSource);

            damageEvent.HandleLogging(attacker, defender);

            return damageEvent;
        }

        private float DoCalculateDamage(Creature attacker, Creature defender, WorldObject damageSource)
        {
            var playerAttacker = attacker as Player;
            var playerDefender = defender as Player;

            var pkBattle = playerAttacker != null && playerDefender != null;

            Attacker = attacker;
            Defender = defender;

            CombatType = damageSource.ProjectileSource == null ? CombatType.Melee : CombatType.Missile;

            DamageSource = damageSource;

            Weapon = damageSource.ProjectileSource == null ? attacker.GetEquippedMeleeWeapon() : (damageSource.ProjectileLauncher ?? damageSource.ProjectileAmmo);

            AttackType = attacker.AttackType;
            AttackHeight = attacker.AttackHeight ?? AttackHeight.Medium;

            // check lifestone protection
            if (playerDefender != null && playerDefender.UnderLifestoneProtection)
            {
                LifestoneProtection = true;
                playerDefender.HandleLifestoneProtection();
                return 0.0f;
            }

            if (defender.Invincible)
                return 0.0f;

            // overpower
            if (attacker.Overpower != null)
                Overpower = Creature.GetOverpower(attacker, defender);

            // evasion chance
            if (!Overpower)
            {
                EvasionChance = GetEvadeChance(attacker, defender);

                // CONQUEST: Melee/Missile augmentation evasion reduction
                // Attacker's augs reduce defender's ability to evade
                if (playerAttacker != null && pkBattle)
                {
                    float meleeAugScale = 0.0010f;   // Melee aug evasion reduction scaling (0.1% per aug)
                    float missileAugScale = 0.0010f; // Missile aug evasion reduction scaling (0.1% per aug)

                    float meleeAug = playerAttacker.LuminanceAugmentMeleeCount ?? 0;
                    float missileAug = playerAttacker.LuminanceAugmentMissileCount ?? 0;

                    // Calculate individual reductions
                    float meleeReduction = Math.Min(meleeAug * meleeAugScale, 0.95f);   // Max 95% reduction
                    float missileReduction = Math.Min(missileAug * missileAugScale, 0.95f); // Max 95% reduction

                    // Apply combined evasion reduction
                    float totalEvasionReduction = 1.0f - ((1.0f - meleeReduction) * (1.0f - missileReduction));
                    EvasionChance *= (1.0f - totalEvasionReduction);
                }
                if (EvasionChance > ThreadSafeRandom.Next(0.0f, 1.0f))
                {
                    Evaded = true;
                    return 0.0f;
                }
            }

            // get base damage
            if (playerAttacker != null)
                GetBaseDamage(playerAttacker);
            else
                GetBaseDamage(attacker, AttackMotion ?? MotionCommand.Invalid, AttackHook);

            // Apply enrage multiplier if the attacker is a mob and enraged
            if (attacker.IsEnraged && !(attacker is Player))
            {
                var enrageMultiplier = attacker.EnrageDamageMultiplier ?? 1.0f;
                BaseDamage *= enrageMultiplier;
            }

            if (DamageType == DamageType.Undef)
            {
                if ((attacker?.Guid.IsPlayer() ?? false) || (damageSource?.Guid.IsPlayer() ?? false))
                {
                    log.Error($"DamageEvent.DoCalculateDamage({attacker?.Name} ({attacker?.Guid}), {defender?.Name} ({defender?.Guid}), {damageSource?.Name} ({damageSource?.Guid})) - DamageType == DamageType.Undef");
                    GeneralFailure = true;
                }
            }

            if (GeneralFailure) return 0.0f;

            // CONQUEST: Melee/Missile augmentation flat damage bonus
            var isMissile = Weapon != null && Weapon.IsMissileWeapon;
            long damageBonus = 0;
            if (isMissile && attacker.LuminanceAugmentMissileCount > 0)
            {
                damageBonus = attacker.LuminanceAugmentMissileCount.Value;
            }
            else if (!isMissile && attacker.LuminanceAugmentMeleeCount > 0)
            {
                damageBonus = attacker.LuminanceAugmentMeleeCount.Value;
            }

            BaseDamage += damageBonus;

            // get damage modifiers
            PowerMod = attacker.GetPowerMod(Weapon);
            AttributeMod = attacker.GetAttributeMod(Weapon);
            SlayerMod = WorldObject.GetWeaponCreatureSlayerModifier(Weapon, attacker, defender);

            // ratings
            DamageRatingBaseMod = Creature.GetPositiveRatingMod(attacker.GetDamageRating());
            RecklessnessMod = Creature.GetRecklessnessMod(attacker, defender);
            SneakAttackMod = attacker.GetSneakAttackMod(defender);
            HeritageMod = attacker.GetHeritageBonus(Weapon) ? 1.05f : 1.0f;

            DamageRatingMod = Creature.AdditiveCombine(DamageRatingBaseMod, RecklessnessMod, SneakAttackMod, HeritageMod);

            if (pkBattle)
            {
                PkDamageMod = Creature.GetPositiveRatingMod(attacker.GetPKDamageRating());
                DamageRatingMod = Creature.AdditiveCombine(DamageRatingMod, PkDamageMod);
            }

            // damage before mitigation
            DamageBeforeMitigation = BaseDamage * AttributeMod * PowerMod * SlayerMod * DamageRatingMod;

            // critical hit?
            var attackSkill = attacker.GetCreatureSkill(attacker.GetCurrentWeaponSkill());
            CriticalChance = WorldObject.GetWeaponCriticalChance(Weapon, attacker, attackSkill, defender);

            // https://asheron.fandom.com/wiki/Announcements_-_2002/08_-_Atonement
            // It should be noted that any time a character is logging off, PK or not, all physical attacks against them become automatically critical.
            // (Note that spells do not share this behavior.) We hope this will stress the need to log off in a safe place.

            if (playerDefender != null && (playerDefender.IsLoggingOut || playerDefender.PKLogout))
                CriticalChance = 1.0f;

            // CONQUEST: Melee/Missile augmentation critical damage bonus
            // Define configurable parameter for critical damage adjustment (default 0.015 = 1.5% per aug)
            // Skip in PvP if pvp_disable_custom_augs is enabled
            float luminanceAugmentCritDamageMultiplier = 0.015f;
            float luminanceAugmentBonus = 0;

            bool skipPvPMeleeAugs = pkBattle && PropertyManager.GetBool("pvp_disable_custom_augs");

            if (!skipPvPMeleeAugs)
            {
                switch (CombatType)
                {
                    case CombatType.Melee:
                        if (attacker.LuminanceAugmentMeleeCount > 0)
                        {
                            luminanceAugmentBonus = attacker.LuminanceAugmentMeleeCount.Value * luminanceAugmentCritDamageMultiplier;
                        }
                        break;

                    case CombatType.Missile:
                        if (attacker.LuminanceAugmentMissileCount > 0)
                        {
                            luminanceAugmentBonus = attacker.LuminanceAugmentMissileCount.Value * luminanceAugmentCritDamageMultiplier;
                        }
                        break;
                }
            }

            if (CriticalChance > ThreadSafeRandom.Next(0.0f, 1.0f))
            {
                if (playerDefender != null && playerDefender.AugmentationCriticalDefense > 0)
                {
                    var criticalDefenseMod = playerAttacker != null ? 0.05f : 0.25f;
                    var criticalDefenseChance = playerDefender.AugmentationCriticalDefense * criticalDefenseMod;

                    if (criticalDefenseChance > ThreadSafeRandom.Next(0.0f, 1.0f))
                        CriticalDefended = true;
                }

                if (!CriticalDefended)
                {
                    IsCritical = true;

                    // verify: CriticalMultiplier only applied to the additional crit damage,
                    // whereas CD/CDR applied to the total damage (base damage + additional crit damage)
                    // CONQUEST: Include melee/missile augmentation critical damage bonus
                    CriticalDamageMod = 1.0f + WorldObject.GetWeaponCritDamageMod(Weapon, attacker, attackSkill, defender) + luminanceAugmentBonus;

                    // Apply enrage multiplier if attacker is a mob and enraged
                    if (attacker.IsEnraged && !(attacker is Player))
                    {
                        CriticalDamageMod *= attacker.EnrageDamageMultiplier ?? 1.0f;
                    }

                    CriticalDamageRatingMod = Creature.GetPositiveRatingMod(attacker.GetCritDamageRating());

                    // recklessness excluded from crits
                    RecklessnessMod = 1.0f;
                    DamageRatingMod = Creature.AdditiveCombine(DamageRatingBaseMod, CriticalDamageRatingMod, SneakAttackMod, HeritageMod);

                    if (pkBattle)
                        DamageRatingMod = Creature.AdditiveCombine(DamageRatingMod, PkDamageMod);

                    DamageBeforeMitigation = BaseDamageMod.MaxDamage * AttributeMod * PowerMod * SlayerMod * DamageRatingMod * CriticalDamageMod;
                }
            }

            // armor rending and cleaving
            var armorRendingMod = 1.0f;
            if (Weapon != null && Weapon.HasImbuedEffect(ImbuedEffectType.ArmorRending))
                armorRendingMod = WorldObject.GetArmorRendingMod(attackSkill);

            var armorCleavingMod = attacker.GetArmorCleavingMod(Weapon);

            // Apply PvP handling for Armor Cleaving (weapon property)
            var isPvPCleave = attacker is Player && defender is Player;
            if (isPvPCleave && armorCleavingMod < 1.0f)
            {
                // CONQUEST: Disable Armor Cleaving entirely in PvP - only Armor Rending imbue works
                if (PropertyManager.GetBool("pvp_disable_armor_cleaving"))
                {
                    armorCleavingMod = 1.0f;
                }
                else
                {
                    var pvpCapCleaving = (float)PropertyManager.GetDouble("pvp_max_armor_cleaving");
                    if (pvpCapCleaving > 0)
                    {
                        // armorCleavingMod is inverted (lower = more armor ignored), so we take Max
                        // e.g. 0.3 means 70% armor ignored, cap of 0.5 means max 50% ignored
                        armorCleavingMod = Math.Max(armorCleavingMod, 1.0f - pvpCapCleaving);
                    }
                }
            }

            var ignoreArmorMod = Math.Min(armorRendingMod, armorCleavingMod);

            // get body part / armor pieces / armor modifier
            if (playerDefender != null)
            {
                // select random body part @ current attack height
                GetBodyPart(AttackHeight);

                // get player armor pieces
                Armor = attacker.GetArmorLayers(playerDefender, BodyPart);

                // get armor modifiers
                ArmorMod = attacker.GetArmorMod(playerDefender, DamageType, Armor, Weapon, ignoreArmorMod);

                // Capture bodyArmorMod for debug output (Armor Self / Imperil effects)
                BodyArmorMod = playerDefender.EnchantmentManager.GetBodyArmorMod();
            }
            else
            {
                // determine height quadrant
                Quadrant = GetQuadrant(Defender, Attacker, AttackHeight, DamageSource);

                // select random body part @ current attack height
                GetBodyPart(Defender, Quadrant);
                if (Evaded)
                    return 0.0f;

                Armor = CreaturePart.GetArmorLayers(PropertiesBodyPart.Key);

                // get target armor
                ArmorMod = CreaturePart.GetArmorMod(DamageType, Armor, Attacker, Weapon, ignoreArmorMod);
            }

            // CONQUEST: Disable IgnoreAllArmor (Phantom weapons) in PvP if configured
            if (Weapon != null && Weapon.HasImbuedEffect(ImbuedEffectType.IgnoreAllArmor))
            {
                if (!pkBattle || !PropertyManager.GetBool("pvp_disable_ignore_all_armor"))
                    ArmorMod = 1.0f;
            }

            // CONQUEST: Apply synthetic nether armor override in PvP
            // Since players cannot get Nether Bane spells or tinker ArmorModVsNether,
            // this REPLACES the ArmorMod entirely to simulate properly protected armor
            if (pkBattle && DamageType == DamageType.Nether && PropertyManager.GetBool("pvp_nether_protection_enabled"))
            {
                var armorOverride = GetNetherArmorOverrideForWeapon(Weapon);
                if (armorOverride > 0)
                {
                    // Convert the override ArmorMod to effective AL, apply Imperil, then convert back
                    // ArmorMod = 66.67 / (AL + 66.67) for positive AL
                    // So AL = 66.67 * (1 - ArmorMod) / ArmorMod
                    const float ArmorModConstant = 200.0f / 3.0f; // ~66.67
                    float syntheticAL = ArmorModConstant * (1.0f - armorOverride) / armorOverride;

                    // Apply Imperil/Armor enchantments (negative = Imperil, positive = Armor Self)
                    syntheticAL += BodyArmorMod;

                    // Apply extremity penalty for head/hands/feet (reduces effective AL)
                    var extremityPenalty = (float)PropertyManager.GetDouble("pvp_nether_armor_extremity_penalty");
                    if (extremityPenalty > 0)
                    {
                        var bodyPart = PropertiesBodyPart.Key;
                        if (bodyPart == CombatBodyPart.Head ||
                            bodyPart == CombatBodyPart.Hand ||
                            bodyPart == CombatBodyPart.Foot)
                        {
                            // Convert extremity penalty (ArmorMod delta) to AL reduction
                            syntheticAL -= extremityPenalty * ArmorModConstant;
                        }
                    }

                    // Convert back to ArmorMod using the standard formula
                    ArmorMod = SkillFormula.CalcArmorMod(syntheticAL);
                }
            }

            // get resistance modifiers
            WeaponResistanceMod = WorldObject.GetWeaponResistanceModifier(Weapon, attacker, attackSkill, DamageType, defender);

            if (playerDefender != null)
            {
                ResistanceMod = playerDefender.GetResistanceMod(DamageType, Attacker, Weapon, WeaponResistanceMod);

                // CONQUEST: Apply synthetic nether protection in PvP
                // Since players cannot get Nether Protection spells or Nether Wards,
                // this simulates those protections to balance nether weapons against elemental weapons
                if (pkBattle && DamageType == DamageType.Nether && PropertyManager.GetBool("pvp_nether_protection_enabled"))
                {
                    var syntheticProtSpell = (float)PropertyManager.GetDouble("pvp_nether_protection_spell");
                    var syntheticProtWard = (float)PropertyManager.GetDouble("pvp_nether_protection_ward");

                    // Apply synthetic protection spell (simulates Nether Protection VII)
                    // Only apply if it would provide better protection than current
                    if (syntheticProtSpell > 0 && syntheticProtSpell < ResistanceMod)
                        ResistanceMod = syntheticProtSpell;

                    // Apply synthetic ward (simulates Legendary Nether Ward) - multiplicative
                    if (syntheticProtWard > 0 && syntheticProtWard < 1.0f)
                        ResistanceMod *= syntheticProtWard;
                }
            }
            else
            {
                var resistanceType = Creature.GetResistanceType(DamageType);
                ResistanceMod = (float)Math.Max(0.0f, defender.GetResistanceMod(resistanceType, Attacker, Weapon, WeaponResistanceMod));
            }

            // damage resistance rating
            DamageResistanceRatingMod = DamageResistanceRatingBaseMod = defender.GetDamageResistRatingMod(CombatType);

            if (IsCritical)
            {
                CriticalDamageResistanceRatingMod = Creature.GetNegativeRatingMod(defender.GetCritDamageResistRating());

                DamageResistanceRatingMod = Creature.AdditiveCombine(DamageResistanceRatingBaseMod, CriticalDamageResistanceRatingMod);
            }

            if (pkBattle)
            {
                PkDamageResistanceMod = Creature.GetNegativeRatingMod(defender.GetPKDamageResistRating());

                DamageResistanceRatingMod = Creature.AdditiveCombine(DamageResistanceRatingMod, PkDamageResistanceMod);
            }

            // get shield modifier
            ShieldMod = defender.GetShieldMod(attacker, DamageType, Weapon);

            // calculate final output damage
            Damage = DamageBeforeMitigation * ArmorMod * ShieldMod * ResistanceMod * DamageResistanceRatingMod;

            // ===================================================================================
            // PvP Damage Configuration System - Apply granular PvP damage modifiers
            // ===================================================================================
            if (pkBattle && Weapon != null)
            {
                float pvpConfigMod = 1.0f;
                var weaponSkill = attacker.GetCurrentWeaponSkill();

                // Step 1: Determine weapon type prefix for property lookups
                string weaponPrefix = GetPvPWeaponPrefix(weaponSkill, Weapon);

                // Step 2: Apply base weapon type modifier
                pvpConfigMod = (float)PropertyManager.GetDouble($"pvp_dmg_mod_{weaponPrefix}");

                // Step 3: Apply special attack type modifiers (triple strike, multi-strike)
                if (weaponSkill == Skill.LightWeapons && AttackType == AttackType.TripleStrike)
                {
                    pvpConfigMod *= (float)PropertyManager.GetDouble("pvp_dmg_mod_lw_triplestrike");
                    if (IsCritical && Weapon.HasImbuedEffect(ImbuedEffectType.CripplingBlow))
                        pvpConfigMod *= (float)PropertyManager.GetDouble("pvp_dmg_mod_lw_cb_crit_triplestrike");
                }
                else if (weaponSkill == Skill.HeavyWeapons && AttackType.HasFlag(AttackType.MultiStrike))
                {
                    pvpConfigMod *= (float)PropertyManager.GetDouble("pvp_dmg_mod_hw_multistrike");
                    if (IsCritical && Weapon.HasImbuedEffect(ImbuedEffectType.CripplingBlow))
                        pvpConfigMod *= (float)PropertyManager.GetDouble("pvp_dmg_mod_hw_cb_crit_multistrike");
                }

                // Step 4: Apply imbue effect modifiers (multiplicative)
                pvpConfigMod *= GetPvPImbueMod(Weapon, weaponPrefix, IsCritical);

                // Step 5: Apply nether damage multiplier if this is nether damage
                // This compensates for lack of Nether Vuln spells and Asheron's Protection
                if (DamageType == DamageType.Nether)
                {
                    var netherDamageMod = GetNetherDamageModForWeapon(Weapon);
                    pvpConfigMod *= netherDamageMod;

                    // Apply additional nether crit modifier if this is a critical hit
                    if (IsCritical)
                    {
                        var netherCritMod = GetNetherCritModForWeapon(Weapon);
                        pvpConfigMod *= netherCritMod;
                    }
                }

                // Apply final PvP configuration modifier
                Damage *= pvpConfigMod;
            }

            // Apply split arrow damage multiplier if this is a split arrow
            if (DamageSource.GetProperty(PropertyBool.IsSplitArrow) == true)
            {
                var splitMultiplier = (float)(DamageSource.ProjectileLauncher?.GetProperty(PropertyFloat.SplitArrowDamageMultiplier) ??
                                             DefaultSplitArrowDamageMultiplier);
                Damage *= splitMultiplier;
            }

            // Apply enrage damage reduction to the final output damage if the defender is a mob and enraged
            if (defender.IsEnraged && !(defender is Player))
            {
                var damageReduction = defender.EnrageDamageReduction ?? 0.0f; // Default to no reduction
                Damage *= (1.0f - damageReduction); // Apply reduction (e.g., 0.2 = 20% reduction)
            }

            // CONQUEST: Apply PvP damage caps to prevent 1-shot kills from crits
            if (pkBattle && Weapon != null)
            {
                float maxDamage = 0;
                var weaponType = Weapon.W_WeaponType;

                if (CombatType == CombatType.Melee)
                {
                    if (weaponType == WeaponType.TwoHanded)
                        maxDamage = (float)PropertyManager.GetDouble("pvp_max_2h_damage");
                    else
                        maxDamage = (float)PropertyManager.GetDouble("pvp_max_melee_damage");
                }
                else if (CombatType == CombatType.Missile)
                {
                    if (weaponType == WeaponType.Bow)
                        maxDamage = (float)PropertyManager.GetDouble("pvp_max_bow_damage");
                    else if (weaponType == WeaponType.Crossbow)
                        maxDamage = (float)PropertyManager.GetDouble("pvp_max_xbow_damage");
                }

                if (maxDamage > 0 && Damage > maxDamage)
                    Damage = maxDamage;
            }

            DamageMitigated = DamageBeforeMitigation - Damage;

            return Damage;
        }

        public Quadrant GetQuadrant(Creature defender, Creature attacker, AttackHeight attackHeight, WorldObject damageSource)
        {
            var quadrant = attackHeight.ToQuadrant();

            var wo = damageSource.CurrentLandblock != null ? damageSource : attacker;

            quadrant |= wo.GetRelativeDir(defender);

            return quadrant;
        }

        /// <summary>
        /// Returns the chance for creature to avoid monster attack
        /// </summary>
        public float GetEvadeChance(Creature attacker, Creature defender)
        {
            AccuracyMod = attacker.GetAccuracyMod(Weapon);

            EffectiveAttackSkill = attacker.GetEffectiveAttackSkill();

            //var attackType = attacker.GetCombatType();

            EffectiveDefenseSkill = defender.GetEffectiveDefenseSkill(CombatType);

            var evadeChance = 1.0f - SkillCheck.GetSkillChance(EffectiveAttackSkill, EffectiveDefenseSkill);
            return (float)evadeChance;
        }

        /// <summary>
        /// Returns the base damage for a player attacker
        /// </summary>
        public void GetBaseDamage(Player attacker)
        {
            if (DamageSource.ItemType == ItemType.MissileWeapon)
            {
                DamageType = DamageSource.W_DamageType;

                // handle prismatic arrows
                if (DamageType == DamageType.Base)
                {
                    if (Weapon != null && Weapon.W_DamageType != DamageType.Undef)
                        DamageType = Weapon.W_DamageType;
                    else
                        DamageType = DamageType.Pierce;
                }
            }
            else
                DamageType = attacker.GetDamageType(false, CombatType.Melee);

            // TODO: combat maneuvers for player?
            BaseDamageMod = attacker.GetBaseDamageMod(DamageSource);

            // some quest bows can have built-in damage bonus
            if (Weapon?.WeenieType == WeenieType.MissileLauncher)
                BaseDamageMod.DamageBonus += Weapon.Damage ?? 0;

            if (DamageSource.ItemType == ItemType.MissileWeapon)
                BaseDamageMod.ElementalBonus = WorldObject.GetMissileElementalDamageBonus(Weapon, attacker, DamageType);

            BaseDamage = (float)ThreadSafeRandom.Next(BaseDamageMod.MinDamage, BaseDamageMod.MaxDamage);
        }

        /// <summary>
        /// Returns the base damage for a non-player attacker
        /// </summary>
        public void GetBaseDamage(Creature attacker, MotionCommand motionCommand, AttackHook attackHook)
        {
            AttackPart = attacker.GetAttackPart(motionCommand, attackHook);
            if (AttackPart.Value == null)
            {
                GeneralFailure = true;
                return;
            }

            BaseDamageMod = attacker.GetBaseDamage(AttackPart.Value);
            BaseDamage = (float)ThreadSafeRandom.Next(BaseDamageMod.MinDamage, BaseDamageMod.MaxDamage);

            DamageType = attacker.GetDamageType(AttackPart.Value, CombatType);
        }

        /// <summary>
        /// Returns a body part for a player defender
        /// </summary>
        public void GetBodyPart(AttackHeight attackHeight)
        {
            // select random body part @ current attack height
            BodyPart = BodyParts.GetBodyPart(attackHeight);
        }

        public static readonly Quadrant LeftRight = Quadrant.Left | Quadrant.Right;
        public static readonly Quadrant FrontBack = Quadrant.Front | Quadrant.Back;

        /// <summary>
        /// Returns a body part for a creature defender
        /// </summary>
        public void GetBodyPart(Creature defender, Quadrant quadrant)
        {
            // get cached body parts table
            var bodyParts = Creature.GetBodyParts(defender.WeenieClassId);

            if (bodyParts == null)
            {
                Evaded = true;
                return;
            }

            // rng roll for body part
            var bodyPart = bodyParts.RollBodyPart(quadrant);

            if (bodyPart == CombatBodyPart.Undefined)
            {
                log.DebugFormat("DamageEvent.GetBodyPart({0} ({1}) ) - couldn't find body part for wcid {2}, Quadrant {3}", defender?.Name, defender?.Guid, defender.WeenieClassId, quadrant);
                Evaded = true;
                return;
            }

            //Console.WriteLine($"AttackHeight: {AttackHeight}, Quadrant: {quadrant & FrontBack}{quadrant & LeftRight}, AttackPart: {bodyPart}");

            if (!defender.Biota.PropertiesBodyPart.TryGetValue(bodyPart, out var value))

            {
                log.DebugFormat("DamageEvent.GetBodyPart({0} ({1}) ) - couldn't find body part {2} in biota for wcid {3}", defender?.Name, defender?.Guid, bodyPart, defender.WeenieClassId);
                Evaded = true;
                return;
            }

            PropertiesBodyPart = new KeyValuePair<CombatBodyPart, PropertiesBodyPart>(bodyPart, value);

            CreaturePart = new Creature_BodyPart(defender, PropertiesBodyPart);
        }

        public void ShowInfo(Creature creature)
        {
            var targetInfo = PlayerManager.GetOnlinePlayer(creature.DebugDamageTarget);
            if (targetInfo == null)
            {
                creature.DebugDamage = Creature.DebugDamageType.None;
                return;
            }

            // setup
            var info = $"Attacker: {Attacker.Name} ({Attacker.Guid})\n";
            info += $"Defender: {Defender.Name} ({Defender.Guid})\n";

            info += $"CombatType: {CombatType}\n";

            info += $"DamageSource: {DamageSource.Name} ({DamageSource.Guid})\n";
            info += $"DamageType: {DamageType}\n";

            var weaponName = Weapon != null ? $"{Weapon.Name} ({Weapon.Guid})" : "None\n";
            info += $"Weapon: {weaponName}\n";

            info += $"AttackType: {AttackType}\n";
            info += $"AttackHeight: {AttackHeight}\n";

            // lifestone protection
            if (LifestoneProtection)
                info += $"LifestoneProtection: {LifestoneProtection}\n";

            // evade
            if (AccuracyMod != 0.0f && AccuracyMod != 1.0f)
                info += $"AccuracyMod: {AccuracyMod}\n";

            info += $"EffectiveAttackSkill: {EffectiveAttackSkill}\n";
            info += $"EffectiveDefenseSkill: {EffectiveDefenseSkill}\n";

            if (Attacker.Overpower != null)
                info += $"Overpower: {Overpower} ({Creature.GetOverpowerChance(Attacker, Defender)})\n";

            info += $"EvasionChance: {EvasionChance}\n";
            info += $"Evaded: {Evaded}\n";

            if (!(Attacker is Player))
            {
                if (AttackMotion != null)
                    info += $"AttackMotion: {AttackMotion}\n";
                if (AttackPart.Value != null)
                    info += $"AttackPart: {AttackPart.Key}\n";
            }

            // base damage
            if (BaseDamageMod != null)
                info += $"BaseDamageRange: {BaseDamageMod.Range}\n";


            info += $"BaseDamage: {BaseDamage}\n";

            // damage modifiers
            info += $"AttributeMod: {AttributeMod}\n";

            if (PowerMod != 0.0f && PowerMod != 1.0f)
                info += $"PowerMod: {PowerMod}\n";

            if (SlayerMod != 0.0f && SlayerMod != 1.0f)
                info += $"SlayerMod: {SlayerMod}\n";

            if (BaseDamageMod != null)
            {
                if (BaseDamageMod.DamageBonus != 0)
                    info += $"DamageBonus: {BaseDamageMod.DamageBonus}\n";

                if (BaseDamageMod.DamageMod != 0.0f && BaseDamageMod.DamageMod != 1.0f)
                    info += $"DamageMod: {BaseDamageMod.DamageMod}\n";

                if (BaseDamageMod.ElementalBonus != 0)
                    info += $"ElementalDamageBonus: {BaseDamageMod.ElementalBonus}\n";
            }

            // critical hit
            info += $"CriticalChance: {CriticalChance}\n";
            info += $"CriticalHit: {IsCritical}\n";

            if (CriticalDefended)
                info += $"CriticalDefended: {CriticalDefended}\n";

            if (CriticalDamageMod != 0.0f && CriticalDamageMod != 1.0f)
                info += $"CriticalDamageMod: {CriticalDamageMod}\n";

            if (CriticalDamageRatingMod != 0.0f && CriticalDamageRatingMod != 1.0f)
                info += $"CriticalDamageRatingMod: {CriticalDamageRatingMod}\n";

            // damage ratings
            if (DamageRatingBaseMod != 0.0f && DamageRatingBaseMod != 1.0f)
                info += $"DamageRatingBaseMod: {DamageRatingBaseMod}\n";

            if (HeritageMod != 0.0f && HeritageMod != 1.0f)
                info += $"HeritageMod: {HeritageMod}\n";

            if (RecklessnessMod != 0.0f && RecklessnessMod != 1.0f)
                info += $"RecklessnessMod: {RecklessnessMod}\n";

            if (SneakAttackMod != 0.0f && SneakAttackMod != 1.0f)
                info += $"SneakAttackMod: {SneakAttackMod}\n";

            if (PkDamageMod != 0.0f && PkDamageMod != 1.0f)
                info += $"PkDamageMod: {PkDamageMod}\n";

            if (DamageRatingMod != 0.0f && DamageRatingMod != 1.0f)
                info += $"DamageRatingMod: {DamageRatingMod}\n";

            if (BodyPart != 0)
            {
                // player body part
                info += $"BodyPart: {BodyPart}\n";
            }
            if (Armor != null && Armor.Count > 0)
            {
                info += $"Armors: {string.Join(", ", Armor.Select(i => i.Name))}\n";
            }

            if (CreaturePart != null)
            {
                // creature body part
                info += $"BodyPart: {PropertiesBodyPart.Key}\n";
                info += $"BaseArmor: {CreaturePart.Biota.Value.BaseArmor}\n";
            }

            // damage mitigation
            if (ArmorMod != 0.0f && ArmorMod != 1.0f)
                info += $"ArmorMod: {ArmorMod}\n";

            // Show BodyArmorMod (Armor Self / Imperil effect) - always show in debug if non-zero
            if (BodyArmorMod != 0)
                info += $"BodyArmorMod (Imperil/Armor): {BodyArmorMod}\n";

            if (ResistanceMod != 0.0f && ResistanceMod != 1.0f)
                info += $"ResistanceMod: {ResistanceMod}\n";

            if (ShieldMod != 0.0f && ShieldMod != 1.0f)
                info += $"ShieldMod: {ShieldMod}\n";

            if (WeaponResistanceMod != 0.0f && WeaponResistanceMod != 1.0f)
                info += $"WeaponResistanceMod: {WeaponResistanceMod}\n";

            if (DamageResistanceRatingBaseMod != 0.0f && DamageResistanceRatingBaseMod != 1.0f)
                info += $"DamageResistanceRatingBaseMod: {DamageResistanceRatingBaseMod}\n";

            if (CriticalDamageResistanceRatingMod != 0.0f && CriticalDamageResistanceRatingMod != 1.0f)
                info += $"CriticalDamageResistanceRatingMod: {CriticalDamageResistanceRatingMod}\n";

            if (PkDamageResistanceMod != 0.0f && PkDamageResistanceMod != 1.0f)
                info += $"PkDamageResistanceMod: {PkDamageResistanceMod}\n";

            if (DamageResistanceRatingMod != 0.0f && DamageResistanceRatingMod != 1.0f)
                info += $"DamageResistanceRatingMod: {DamageResistanceRatingMod}\n";

            if (IgnoreMagicArmor)
                info += $"IgnoreMagicArmor: {IgnoreMagicArmor}\n";
            if (IgnoreMagicResist)
                info += $"IgnoreMagicResist: {IgnoreMagicResist}\n";

            // final damage
            info += $"DamageBeforeMitigation: {DamageBeforeMitigation}\n";
            info += $"DamageMitigated: {DamageMitigated}\n";
            info += $"Damage: {Damage}\n";

            info += "----";

            targetInfo.Session.Network.EnqueueSend(new GameMessageSystemChat(info, ChatMessageType.Broadcast));
        }

        public void HandleLogging(Creature attacker, Creature defender)
        {
            if (attacker != null && (attacker.DebugDamage & Creature.DebugDamageType.Attacker) != 0)
            {
                ShowInfo(attacker);
            }
            if (defender != null && (defender.DebugDamage & Creature.DebugDamageType.Defender) != 0)
            {
                ShowInfo(defender);
            }
        }

        public AttackConditions AttackConditions
        {
            get
            {
                var attackConditions = new AttackConditions();

                if (CriticalDefended)
                    attackConditions |= AttackConditions.CriticalProtectionAugmentation;
                if (RecklessnessMod > 1.0f)
                    attackConditions |= AttackConditions.Recklessness;
                if (SneakAttackMod > 1.0f)
                    attackConditions |= AttackConditions.SneakAttack;
                if (Overpower)
                    attackConditions |= AttackConditions.Overpower;

                return attackConditions;
            }
        }

        /// <summary>
        /// Returns the weapon prefix string for PvP damage property lookups
        /// </summary>
        private static string GetPvPWeaponPrefix(Skill weaponSkill, WorldObject weapon)
        {
            switch (weaponSkill)
            {
                case Skill.FinesseWeapons:
                    return "fw";
                case Skill.LightWeapons:
                    return "lw";
                case Skill.HeavyWeapons:
                    return "hw";
                case Skill.TwoHandedCombat:
                    return "2h";
                case Skill.MissileWeapons:
                    if (weapon.DefaultCombatStyle == CombatStyle.Bow)
                        return "bow";
                    else if (weapon.DefaultCombatStyle == CombatStyle.Crossbow)
                        return "xbow";
                    else // Thrown weapons and atlatls
                        return "tw";
                default:
                    return "fw"; // Default fallback
            }
        }

        /// <summary>
        /// Calculates the combined PvP imbue modifier for a weapon
        /// </summary>
        private float GetPvPImbueMod(WorldObject weapon, string weaponPrefix, bool isCritical)
        {
            float imbueMod = 1.0f;

            // Armor Rending
            if (weapon.HasImbuedEffect(ImbuedEffectType.ArmorRending))
            {
                imbueMod *= (float)PropertyManager.GetDouble("pvp_dmg_mod_ar");
                imbueMod *= (float)PropertyManager.GetDouble($"pvp_dmg_mod_{weaponPrefix}_ar");
            }

            // Crippling Blow
            if (weapon.HasImbuedEffect(ImbuedEffectType.CripplingBlow))
            {
                imbueMod *= (float)PropertyManager.GetDouble("pvp_dmg_mod_cb");
                imbueMod *= (float)PropertyManager.GetDouble($"pvp_dmg_mod_{weaponPrefix}_cb");
                if (isCritical)
                    imbueMod *= (float)PropertyManager.GetDouble($"pvp_dmg_mod_{weaponPrefix}_cb_crit");
            }

            // Critical Strike
            if (weapon.HasImbuedEffect(ImbuedEffectType.CriticalStrike))
            {
                imbueMod *= (float)PropertyManager.GetDouble("pvp_dmg_mod_cs");
                imbueMod *= (float)PropertyManager.GetDouble($"pvp_dmg_mod_{weaponPrefix}_cs");
            }

            // Hollow weapons (IgnoreMagicArmor + IgnoreMagicResist)
            if (IgnoreMagicArmor && IgnoreMagicResist)
            {
                imbueMod *= (float)PropertyManager.GetDouble("pvp_dmg_mod_hollow");
                imbueMod *= (float)PropertyManager.GetDouble($"pvp_dmg_mod_{weaponPrefix}_hollow");
            }

            // Phantom weapons (IgnoreAllArmor)
            if (weapon.HasImbuedEffect(ImbuedEffectType.IgnoreAllArmor))
            {
                imbueMod *= (float)PropertyManager.GetDouble("pvp_dmg_mod_phantom");
                imbueMod *= (float)PropertyManager.GetDouble($"pvp_dmg_mod_{weaponPrefix}_phantom");
            }

            // Elemental Rending - Slash
            if (weapon.HasImbuedEffect(ImbuedEffectType.SlashRending))
            {
                imbueMod *= (float)PropertyManager.GetDouble("pvp_dmg_mod_slash_rend");
                imbueMod *= (float)PropertyManager.GetDouble($"pvp_dmg_mod_{weaponPrefix}_slash_rend");
            }

            // Elemental Rending - Pierce
            if (weapon.HasImbuedEffect(ImbuedEffectType.PierceRending))
            {
                imbueMod *= (float)PropertyManager.GetDouble("pvp_dmg_mod_pierce_rend");
                imbueMod *= (float)PropertyManager.GetDouble($"pvp_dmg_mod_{weaponPrefix}_pierce_rend");
            }

            // Elemental Rending - Bludgeon
            if (weapon.HasImbuedEffect(ImbuedEffectType.BludgeonRending))
            {
                imbueMod *= (float)PropertyManager.GetDouble("pvp_dmg_mod_bludgeon_rend");
                imbueMod *= (float)PropertyManager.GetDouble($"pvp_dmg_mod_{weaponPrefix}_bludgeon_rend");
            }

            // Elemental Rending - Fire
            if (weapon.HasImbuedEffect(ImbuedEffectType.FireRending))
            {
                imbueMod *= (float)PropertyManager.GetDouble("pvp_dmg_mod_fire_rend");
                imbueMod *= (float)PropertyManager.GetDouble($"pvp_dmg_mod_{weaponPrefix}_fire_rend");
            }

            // Elemental Rending - Cold
            if (weapon.HasImbuedEffect(ImbuedEffectType.ColdRending))
            {
                imbueMod *= (float)PropertyManager.GetDouble("pvp_dmg_mod_cold_rend");
                imbueMod *= (float)PropertyManager.GetDouble($"pvp_dmg_mod_{weaponPrefix}_cold_rend");
            }

            // Elemental Rending - Acid
            if (weapon.HasImbuedEffect(ImbuedEffectType.AcidRending))
            {
                imbueMod *= (float)PropertyManager.GetDouble("pvp_dmg_mod_acid_rend");
                imbueMod *= (float)PropertyManager.GetDouble($"pvp_dmg_mod_{weaponPrefix}_acid_rend");
            }

            // Elemental Rending - Electric
            if (weapon.HasImbuedEffect(ImbuedEffectType.ElectricRending))
            {
                imbueMod *= (float)PropertyManager.GetDouble("pvp_dmg_mod_electric_rend");
                imbueMod *= (float)PropertyManager.GetDouble($"pvp_dmg_mod_{weaponPrefix}_electric_rend");
            }

            // Elemental Rending - Nether
            if (weapon.HasImbuedEffect(ImbuedEffectType.NetherRending))
            {
                imbueMod *= (float)PropertyManager.GetDouble("pvp_dmg_mod_nether_rend");
                imbueMod *= (float)PropertyManager.GetDouble($"pvp_dmg_mod_{weaponPrefix}_nether_rend");
            }

            // Note: Biting Strike and Crushing Blow are not standard ImbuedEffectTypes
            // They would need separate detection if implemented in the game

            return imbueMod;
        }

        /// <summary>
        /// CONQUEST: Gets the nether armor override value for PvP based on weapon type
        /// When > 0, this REPLACES ArmorMod entirely to simulate tinkered armor with nether bane
        /// Returns 0 if no override is configured (use actual armor values)
        /// </summary>
        private static float GetNetherArmorOverrideForWeapon(WorldObject weapon)
        {
            if (weapon == null)
                return 0;

            string configKey = null;

            // Check missile weapons first by CombatStyle
            if (weapon.WeaponSkill == Skill.MissileWeapons)
            {
                switch (weapon.DefaultCombatStyle)
                {
                    case CombatStyle.Bow:
                        configKey = "pvp_nether_armor_override_bow";
                        break;
                    case CombatStyle.Crossbow:
                        configKey = "pvp_nether_armor_override_xbow";
                        break;
                    case CombatStyle.ThrownWeapon:
                    case CombatStyle.ThrownShield:
                        configKey = "pvp_nether_armor_override_thrown";
                        break;
                    case CombatStyle.Atlatl:
                        configKey = "pvp_nether_armor_override_atlatl";
                        break;
                }
            }
            else
            {
                // Check melee weapons by WeaponSkill
                switch (weapon.WeaponSkill)
                {
                    case Skill.HeavyWeapons:
                        configKey = "pvp_nether_armor_override_heavy";
                        break;
                    case Skill.LightWeapons:
                        configKey = "pvp_nether_armor_override_light";
                        break;
                    case Skill.FinesseWeapons:
                        configKey = "pvp_nether_armor_override_finesse";
                        break;
                    case Skill.TwoHandedCombat:
                        configKey = "pvp_nether_armor_override_2h";
                        break;
                }
            }

            if (configKey != null)
                return (float)PropertyManager.GetDouble(configKey);

            return 0;
        }

        /// <summary>
        /// CONQUEST: Gets the nether damage multiplier for PvP based on weapon type
        /// Used to compensate for lack of Nether Vuln spells and Asheron's Protection
        /// Returns 1.0 if no weapon-specific value is configured
        /// </summary>
        private static float GetNetherDamageModForWeapon(WorldObject weapon)
        {
            if (weapon == null)
                return 1.0f;

            string configKey = null;

            // Check missile weapons first by CombatStyle
            if (weapon.WeaponSkill == Skill.MissileWeapons)
            {
                switch (weapon.DefaultCombatStyle)
                {
                    case CombatStyle.Bow:
                        configKey = "pvp_nether_damage_mod_bow";
                        break;
                    case CombatStyle.Crossbow:
                        configKey = "pvp_nether_damage_mod_xbow";
                        break;
                    case CombatStyle.ThrownWeapon:
                    case CombatStyle.ThrownShield:
                        configKey = "pvp_nether_damage_mod_thrown";
                        break;
                    case CombatStyle.Atlatl:
                        configKey = "pvp_nether_damage_mod_atlatl";
                        break;
                }
            }
            else
            {
                // Check melee weapons by WeaponSkill
                switch (weapon.WeaponSkill)
                {
                    case Skill.HeavyWeapons:
                        configKey = "pvp_nether_damage_mod_heavy";
                        break;
                    case Skill.LightWeapons:
                        configKey = "pvp_nether_damage_mod_light";
                        break;
                    case Skill.FinesseWeapons:
                        configKey = "pvp_nether_damage_mod_finesse";
                        break;
                    case Skill.TwoHandedCombat:
                        configKey = "pvp_nether_damage_mod_2h";
                        break;
                }
            }

            if (configKey != null)
                return (float)PropertyManager.GetDouble(configKey);

            return 1.0f;
        }

        /// <summary>
        /// CONQUEST: Gets the nether CRIT damage multiplier for PvP based on weapon type
        /// Applied after the base nether damage mod, only on critical hits
        /// Returns 1.0 if no weapon-specific value is configured
        /// </summary>
        private static float GetNetherCritModForWeapon(WorldObject weapon)
        {
            if (weapon == null)
                return 1.0f;

            string configKey = null;

            // Check missile weapons first by CombatStyle
            if (weapon.WeaponSkill == Skill.MissileWeapons)
            {
                switch (weapon.DefaultCombatStyle)
                {
                    case CombatStyle.Bow:
                        configKey = "pvp_nether_crit_mod_bow";
                        break;
                    case CombatStyle.Crossbow:
                        configKey = "pvp_nether_crit_mod_xbow";
                        break;
                    case CombatStyle.ThrownWeapon:
                    case CombatStyle.ThrownShield:
                        configKey = "pvp_nether_crit_mod_thrown";
                        break;
                    case CombatStyle.Atlatl:
                        configKey = "pvp_nether_crit_mod_atlatl";
                        break;
                }
            }
            else
            {
                // Check melee weapons by WeaponSkill
                switch (weapon.WeaponSkill)
                {
                    case Skill.HeavyWeapons:
                        configKey = "pvp_nether_crit_mod_heavy";
                        break;
                    case Skill.LightWeapons:
                        configKey = "pvp_nether_crit_mod_light";
                        break;
                    case Skill.FinesseWeapons:
                        configKey = "pvp_nether_crit_mod_finesse";
                        break;
                    case Skill.TwoHandedCombat:
                        configKey = "pvp_nether_crit_mod_2h";
                        break;
                }
            }

            if (configKey != null)
                return (float)PropertyManager.GetDouble(configKey);

            return 1.0f;
        }
    }
}
