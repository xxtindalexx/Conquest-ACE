CREATE TABLE IF NOT EXISTS `npk_dungeon_landblocks` (
  `landblock` smallint unsigned NOT NULL COMMENT 'Landblock ID (e.g., 0x002B)',
  `variation` int NOT NULL COMMENT 'Variation/variant number (0 = base, 1+ = variants)',
  `description` varchar(255) DEFAULT NULL COMMENT 'Optional admin description',
  PRIMARY KEY (`landblock`, `variation`)
) COMMENT='CONQUEST: NPK-only dungeon landblock+variant combinations';
