using System;
using System.Threading.Tasks;

using ACE.Common;
using ACE.Database.Models.Auth;
using ACE.Database.Models.Shard;

using log4net;

using Microsoft.EntityFrameworkCore;

namespace ACE.Database
{
    /// <summary>
    /// Lightweight COUNT+1 rank lookups for /top placement (players outside the cached top 25).
    /// </summary>
    public static class LeaderboardRankDatabase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(LeaderboardRankDatabase));

        private const int PropLevel = 25;
        private const int PropNumDeaths = 43;
        private const int PropEnlightenment = 390;
        private const int PropNumTitles = 262;
        private const int PropIsMule = 131;
        private const int PropExcludeFromLeaderboards = 9011;
        private const int PropEnlightenmentTimestamp = 8106;
        private const int PropBankedPyreals = 9004;
        private const int PropBankedLuminance = 9005;
        private const int PropLumAugCreature = 9007;
        private const int PropLumAugItem = 9008;
        private const int PropLumAugLife = 9009;
        private const int PropLumAugVoid = 9010;
        private const int PropLumAugWar = 9011;
        private const int PropLumAugDuration = 9016;
        private const int PropLumAugSpecialize = 9017;
        private const int PropLumAugSummon = 9018;
        private const int PropLumAugMelee = 9022;
        private const int PropLumAugMissile = 9023;
        private const int PropLumAugMeleeDef = 9024;
        private const int PropLumAugMissileDef = 9025;
        private const int PropLumAugMagicDef = 9026;

        private static string CharacterEligibilityFilter =>
            $@"c.is_Deleted = 0
  AND NOT EXISTS (
      SELECT 1 FROM biota_properties_bool mule
      WHERE mule.object_Id = c.id AND mule.`type` = {PropIsMule} AND mule.value = 1
  )
  AND NOT EXISTS (
      SELECT 1 FROM biota_properties_bool ex
      WHERE ex.object_Id = c.id AND ex.`type` = {PropExcludeFromLeaderboards} AND ex.value = 1
  )";

        private static string LumAugSumExpression =>
            @"COALESCE(lum_creature.value, 0) + COALESCE(lum_item.value, 0) + COALESCE(lum_life.value, 0) +
                COALESCE(lum_void.value, 0) + COALESCE(lum_war.value, 0) + COALESCE(lum_duration.value, 0) +
                COALESCE(lum_specialize.value, 0) + COALESCE(lum_summon.value, 0) + COALESCE(lum_melee.value, 0) +
                COALESCE(lum_missile.value, 0) + COALESCE(lum_melee_def.value, 0) + COALESCE(lum_missile_def.value, 0) +
                COALESCE(lum_magic_def.value, 0)";

        private static string LumAugJoins =>
            $@"LEFT JOIN biota_properties_int64 lum_creature ON c.id = lum_creature.object_Id AND lum_creature.`type` = {PropLumAugCreature}
LEFT JOIN biota_properties_int64 lum_item ON c.id = lum_item.object_Id AND lum_item.`type` = {PropLumAugItem}
LEFT JOIN biota_properties_int64 lum_life ON c.id = lum_life.object_Id AND lum_life.`type` = {PropLumAugLife}
LEFT JOIN biota_properties_int64 lum_void ON c.id = lum_void.object_Id AND lum_void.`type` = {PropLumAugVoid}
LEFT JOIN biota_properties_int64 lum_war ON c.id = lum_war.object_Id AND lum_war.`type` = {PropLumAugWar}
LEFT JOIN biota_properties_int64 lum_duration ON c.id = lum_duration.object_Id AND lum_duration.`type` = {PropLumAugDuration}
LEFT JOIN biota_properties_int64 lum_specialize ON c.id = lum_specialize.object_Id AND lum_specialize.`type` = {PropLumAugSpecialize}
LEFT JOIN biota_properties_int64 lum_summon ON c.id = lum_summon.object_Id AND lum_summon.`type` = {PropLumAugSummon}
LEFT JOIN biota_properties_int64 lum_melee ON c.id = lum_melee.object_Id AND lum_melee.`type` = {PropLumAugMelee}
LEFT JOIN biota_properties_int64 lum_missile ON c.id = lum_missile.object_Id AND lum_missile.`type` = {PropLumAugMissile}
LEFT JOIN biota_properties_int64 lum_melee_def ON c.id = lum_melee_def.object_Id AND lum_melee_def.`type` = {PropLumAugMeleeDef}
LEFT JOIN biota_properties_int64 lum_missile_def ON c.id = lum_missile_def.object_Id AND lum_missile_def.`type` = {PropLumAugMissileDef}
LEFT JOIN biota_properties_int64 lum_magic_def ON c.id = lum_magic_def.object_Id AND lum_magic_def.`type` = {PropLumAugMagicDef}";

        public static async Task<LeaderboardPlacement> GetPlacementAsync(string category, long score, long tieBreak = 0, double tieBreakFloat = double.MaxValue)
        {
            try
            {
                var rank = category switch
                {
                    "level" => await GetLevelRankAsync((int)score, (int)tieBreak),
                    "enl" => await GetEnlightenmentRankAsync((int)score, tieBreakFloat),
                    "bank" => await GetSimpleInt64RankAsync(PropBankedPyreals, score),
                    "lum" => await GetSimpleInt64RankAsync(PropBankedLuminance, score),
                    "augs" => await GetAugmentsRankAsync(score),
                    "deaths" => await GetSimpleIntRankAsync(PropNumDeaths, (int)score),
                    "titles" => await GetSimpleIntRankAsync(PropNumTitles, (int)score),
                    "qb" => await GetQuestBonusRankAsync(score),
                    _ => 0
                };

                if (rank <= 0)
                    return null;

                return new LeaderboardPlacement
                {
                    Rank = rank,
                    Score = (ulong)score
                };
            }
            catch (Exception ex)
            {
                log.Error($"Failed to get leaderboard placement for category {category}: {ex.Message}");
                return null;
            }
        }

        private static async Task<int> GetLevelRankAsync(int level, int enlightenment)
        {
            var sql = $@"
SELECT COUNT(*) + 1 AS Value
FROM `character` c
LEFT JOIN biota_properties_int level_prop ON c.id = level_prop.object_Id AND level_prop.`type` = {PropLevel}
LEFT JOIN biota_properties_int enl ON c.id = enl.object_Id AND enl.`type` = {PropEnlightenment}
WHERE {CharacterEligibilityFilter}
  AND (
      COALESCE(level_prop.value, 1) > {{0}}
      OR (COALESCE(level_prop.value, 1) = {{0}} AND COALESCE(enl.value, 0) > {{1}})
  )";

            return await ExecuteCountAsync(sql, level, enlightenment);
        }

        private static async Task<int> GetEnlightenmentRankAsync(int enlightenment, double enlightenmentTimestamp)
        {
            var sql = $@"
SELECT COUNT(*) + 1 AS Value
FROM `character` c
LEFT JOIN biota_properties_int enl ON c.id = enl.object_Id AND enl.`type` = {PropEnlightenment}
LEFT JOIN biota_properties_float enl_ts ON c.id = enl_ts.object_Id AND enl_ts.`type` = {PropEnlightenmentTimestamp}
WHERE {CharacterEligibilityFilter}
  AND (
      COALESCE(enl.value, 0) > {{0}}
      OR (COALESCE(enl.value, 0) = {{0}} AND COALESCE(enl_ts.value, 9999999999) < {{1}})
  )";

            return await ExecuteCountAsync(sql, enlightenment, enlightenmentTimestamp);
        }

        private static async Task<int> GetSimpleInt64RankAsync(int propertyType, long score)
        {
            var sql = $@"
SELECT COUNT(*) + 1 AS Value
FROM `character` c
LEFT JOIN biota_properties_int64 score_prop ON c.id = score_prop.object_Id AND score_prop.`type` = {propertyType}
WHERE {CharacterEligibilityFilter}
  AND COALESCE(score_prop.value, 0) > {{0}}";

            return await ExecuteCountAsync(sql, score);
        }

        private static async Task<int> GetSimpleIntRankAsync(int propertyType, int score)
        {
            var sql = $@"
SELECT COUNT(*) + 1 AS Value
FROM `character` c
LEFT JOIN biota_properties_int score_prop ON c.id = score_prop.object_Id AND score_prop.`type` = {propertyType}
WHERE {CharacterEligibilityFilter}
  AND COALESCE(score_prop.value, 0) > {{0}}";

            return await ExecuteCountAsync(sql, score);
        }

        private static async Task<int> GetAugmentsRankAsync(long score)
        {
            var sql = $@"
SELECT COUNT(*) + 1 AS Value
FROM `character` c
{LumAugJoins}
WHERE {CharacterEligibilityFilter}
  AND ({LumAugSumExpression}) > {{0}}";

            return await ExecuteCountAsync(sql, score);
        }

        private static async Task<int> GetQuestBonusRankAsync(long score)
        {
            var authDatabase = ConfigManager.Config.MySql.Authentication.Database;
            var sql = $@"
SELECT COUNT(*) + 1 AS Value
FROM `character` c
WHERE {CharacterEligibilityFilter}
  AND (
      SELECT COUNT(*) + SUM(CASE WHEN aq.num_Times_Completed >= 1 THEN 1 ELSE 0 END)
      FROM `{authDatabase}`.account_quest aq
      WHERE aq.accountId = c.account_Id
        AND aq.quest NOT LIKE 'PKSoulLoot_%'
        AND aq.quest NOT LIKE '!%'
  ) > {{0}}";

            return await ExecuteCountAsync(sql, score);
        }

        private static async Task<int> ExecuteCountAsync(string sql, params object[] parameters)
        {
            using var context = new ShardDbContext();
            var result = await context.Database
                .SqlQueryRaw<int>(sql, parameters)
                .FirstOrDefaultAsync();
            return result;
        }
    }
}
