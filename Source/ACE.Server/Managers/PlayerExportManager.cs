using System;
using System.Data;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using ACE.Common;
using Discord;
using log4net;
using MySqlConnector;

namespace ACE.Server.Managers
{
    /// <summary>
    /// CONQUEST: Handles exporting player data to CSV for analysis
    /// Exports directly from MySQL to avoid server lag
    /// </summary>
    public static class PlayerExportManager
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Export all player data to CSV and upload to Discord
        /// Runs in background thread to avoid server lag
        /// </summary>
        public static void ExportPlayersAsync(string requestedBy)
        {
            Task.Run(() =>
            {
                try
                {
                    var channelId = ConfigManager.Config.Chat.SoloExportsChannelId;
                    if (channelId == 0)
                    {
                        log.Error("[PlayerExport] SoloExportsChannelId not configured in Config.js");
                        return;
                    }

                    if (!ConfigManager.Config.Chat.EnableDiscordConnection)
                    {
                        log.Error("[PlayerExport] Discord connection is not enabled");
                        return;
                    }

                    log.Info($"[PlayerExport] Starting player export requested by {requestedBy}...");

                    var startTime = DateTime.UtcNow;

                    // Build connection string for shard database
                    var shardConfig = ConfigManager.Config.MySql.Shard;
                    var authConfig = ConfigManager.Config.MySql.Authentication;

                    var shardConnectionString = $"server={shardConfig.Host};port={shardConfig.Port};user={shardConfig.Username};password={shardConfig.Password};database={shardConfig.Database};{shardConfig.ConnectionOptions}";

                    // Execute query and build CSV
                    var csv = ExecuteExportQuery(shardConnectionString, authConfig.Database);

                    if (string.IsNullOrEmpty(csv))
                    {
                        log.Error("[PlayerExport] Export query returned no data");
                        return;
                    }

                    // Create file attachment
                    var fileName = $"player_export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
                    var fileBytes = Encoding.UTF8.GetBytes(csv);

                    using (var stream = new MemoryStream(fileBytes))
                    {
                        var fileAttachment = new FileAttachment(stream, fileName);
                        DiscordChatManager.SendDiscordFile(
                            "[PlayerExport]",
                            $"Player export requested by {requestedBy}",
                            channelId,
                            fileAttachment
                        );
                    }

                    var elapsed = (DateTime.UtcNow - startTime).TotalSeconds;
                    log.Info($"[PlayerExport] Export completed in {elapsed:F2}s. File: {fileName}");
                }
                catch (Exception ex)
                {
                    log.Error($"[PlayerExport] Export failed: {ex.Message}", ex);
                }
            });
        }

        private static string ExecuteExportQuery(string shardConnectionString, string authDatabase)
        {
            var sql = BuildExportQuery(authDatabase);

            using (var connection = new MySqlConnection(shardConnectionString))
            {
                connection.Open();

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.CommandTimeout = 300; // 5 minute timeout for large datasets

                    using (var reader = command.ExecuteReader())
                    {
                        return BuildCsvFromReader(reader);
                    }
                }
            }
        }

        private static string BuildExportQuery(string authDatabase)
        {
            // PropertyInt type IDs
            const int PROP_LEVEL = 25;
            const int PROP_CREATION_TIMESTAMP = 98;
            const int PROP_ENLIGHTENMENT = 390;

            // PropertyInt64 type IDs - Custom Lum Augs
            const int PROP_LUMAUG_CREATURE = 9007;
            const int PROP_LUMAUG_ITEM = 9008;
            const int PROP_LUMAUG_LIFE = 9009;
            const int PROP_LUMAUG_VOID = 9010;
            const int PROP_LUMAUG_WAR = 9011;
            const int PROP_LUMAUG_DURATION = 9016;
            const int PROP_LUMAUG_SPECIALIZE = 9017;
            const int PROP_LUMAUG_SUMMON = 9018;
            const int PROP_LUMAUG_MELEE = 9022;
            const int PROP_LUMAUG_MISSILE = 9023;
            const int PROP_LUMAUG_MELEE_DEF = 9024;
            const int PROP_LUMAUG_MISSILE_DEF = 9025;
            const int PROP_LUMAUG_MAGIC_DEF = 9026;

            // PropertyInt64 type IDs - Bank
            const int PROP_BANKED_PYREALS = 9004;
            const int PROP_BANKED_LUMINANCE = 9005;
            const int PROP_BANKED_LEGENDARY_KEYS = 9015;
            const int PROP_CONQUEST_COINS = 9028;
            const int PROP_SOUL_FRAGMENTS = 9029;
            const int PROP_EVENT_TOKENS = 9030;

            // PropertyBool type ID for IsMule (PropertyBool.IsMule = 131)
            const int PROP_IS_MULE = 131;

            return $@"
SELECT
    a.accountName AS 'Account Name',
    a.accountId AS 'Account ID',
    INET_NTOA(CONV(HEX(a.last_Login_I_P), 16, 10)) AS 'Last Login IP',
    c.name AS 'Character Name',
    c.id AS 'Character ID',
    FROM_UNIXTIME(creation.value) AS 'Date of Birth',
    COALESCE(level.value, 1) AS 'Level',
    COALESCE(enl.value, 0) AS 'Enlightenment',
    -- Custom Lum Augs
    COALESCE(lum_creature.value, 0) AS 'LumAug Creature',
    COALESCE(lum_item.value, 0) AS 'LumAug Item',
    COALESCE(lum_life.value, 0) AS 'LumAug Life',
    COALESCE(lum_void.value, 0) AS 'LumAug Void',
    COALESCE(lum_war.value, 0) AS 'LumAug War',
    COALESCE(lum_duration.value, 0) AS 'LumAug Duration',
    COALESCE(lum_specialize.value, 0) AS 'LumAug Specialize',
    COALESCE(lum_summon.value, 0) AS 'LumAug Summon',
    COALESCE(lum_melee.value, 0) AS 'LumAug Melee',
    COALESCE(lum_missile.value, 0) AS 'LumAug Missile',
    COALESCE(lum_melee_def.value, 0) AS 'LumAug Melee Defense',
    COALESCE(lum_missile_def.value, 0) AS 'LumAug Missile Defense',
    COALESCE(lum_magic_def.value, 0) AS 'LumAug Magic Defense',
    -- Bank Contents
    COALESCE(bank_pyr.value, 0) AS 'Banked Pyreals',
    COALESCE(bank_lum.value, 0) AS 'Banked Luminance',
    COALESCE(bank_keys.value, 0) AS 'Banked Legendary Keys',
    COALESCE(coins.value, 0) AS 'Conquest Coins',
    COALESCE(souls.value, 0) AS 'Soul Fragments',
    COALESCE(events.value, 0) AS 'Event Tokens'
FROM `character` c
JOIN `{authDatabase}`.account a ON c.account_Id = a.accountId
-- PropertyInt joins
LEFT JOIN biota_properties_int level ON c.id = level.object_Id AND level.`type` = {PROP_LEVEL}
LEFT JOIN biota_properties_int creation ON c.id = creation.object_Id AND creation.`type` = {PROP_CREATION_TIMESTAMP}
LEFT JOIN biota_properties_int enl ON c.id = enl.object_Id AND enl.`type` = {PROP_ENLIGHTENMENT}
-- Custom Lum Aug joins (PropertyInt64)
LEFT JOIN biota_properties_int64 lum_creature ON c.id = lum_creature.object_Id AND lum_creature.`type` = {PROP_LUMAUG_CREATURE}
LEFT JOIN biota_properties_int64 lum_item ON c.id = lum_item.object_Id AND lum_item.`type` = {PROP_LUMAUG_ITEM}
LEFT JOIN biota_properties_int64 lum_life ON c.id = lum_life.object_Id AND lum_life.`type` = {PROP_LUMAUG_LIFE}
LEFT JOIN biota_properties_int64 lum_void ON c.id = lum_void.object_Id AND lum_void.`type` = {PROP_LUMAUG_VOID}
LEFT JOIN biota_properties_int64 lum_war ON c.id = lum_war.object_Id AND lum_war.`type` = {PROP_LUMAUG_WAR}
LEFT JOIN biota_properties_int64 lum_duration ON c.id = lum_duration.object_Id AND lum_duration.`type` = {PROP_LUMAUG_DURATION}
LEFT JOIN biota_properties_int64 lum_specialize ON c.id = lum_specialize.object_Id AND lum_specialize.`type` = {PROP_LUMAUG_SPECIALIZE}
LEFT JOIN biota_properties_int64 lum_summon ON c.id = lum_summon.object_Id AND lum_summon.`type` = {PROP_LUMAUG_SUMMON}
LEFT JOIN biota_properties_int64 lum_melee ON c.id = lum_melee.object_Id AND lum_melee.`type` = {PROP_LUMAUG_MELEE}
LEFT JOIN biota_properties_int64 lum_missile ON c.id = lum_missile.object_Id AND lum_missile.`type` = {PROP_LUMAUG_MISSILE}
LEFT JOIN biota_properties_int64 lum_melee_def ON c.id = lum_melee_def.object_Id AND lum_melee_def.`type` = {PROP_LUMAUG_MELEE_DEF}
LEFT JOIN biota_properties_int64 lum_missile_def ON c.id = lum_missile_def.object_Id AND lum_missile_def.`type` = {PROP_LUMAUG_MISSILE_DEF}
LEFT JOIN biota_properties_int64 lum_magic_def ON c.id = lum_magic_def.object_Id AND lum_magic_def.`type` = {PROP_LUMAUG_MAGIC_DEF}
-- Bank joins (PropertyInt64)
LEFT JOIN biota_properties_int64 bank_pyr ON c.id = bank_pyr.object_Id AND bank_pyr.`type` = {PROP_BANKED_PYREALS}
LEFT JOIN biota_properties_int64 bank_lum ON c.id = bank_lum.object_Id AND bank_lum.`type` = {PROP_BANKED_LUMINANCE}
LEFT JOIN biota_properties_int64 bank_keys ON c.id = bank_keys.object_Id AND bank_keys.`type` = {PROP_BANKED_LEGENDARY_KEYS}
LEFT JOIN biota_properties_int64 coins ON c.id = coins.object_Id AND coins.`type` = {PROP_CONQUEST_COINS}
LEFT JOIN biota_properties_int64 souls ON c.id = souls.object_Id AND souls.`type` = {PROP_SOUL_FRAGMENTS}
LEFT JOIN biota_properties_int64 events ON c.id = events.object_Id AND events.`type` = {PROP_EVENT_TOKENS}
WHERE c.is_Deleted = 0
  AND NOT EXISTS (
      SELECT 1 FROM biota_properties_bool mule
      WHERE mule.object_Id = c.id AND mule.`type` = {PROP_IS_MULE} AND mule.value = 1
  )
ORDER BY a.accountName, c.name
";
        }

        private static string BuildCsvFromReader(MySqlDataReader reader)
        {
            var sb = new StringBuilder();

            // Write header row
            for (int i = 0; i < reader.FieldCount; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(EscapeCsvField(reader.GetName(i)));
            }
            sb.AppendLine();

            // Write data rows
            while (reader.Read())
            {
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    if (i > 0) sb.Append(',');

                    if (reader.IsDBNull(i))
                    {
                        sb.Append("");
                    }
                    else
                    {
                        sb.Append(EscapeCsvField(reader.GetValue(i).ToString()));
                    }
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private static string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field))
                return "";

            // If field contains comma, quote, or newline, wrap in quotes and escape internal quotes
            if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
            {
                return $"\"{field.Replace("\"", "\"\"")}\"";
            }

            return field;
        }
    }
}
