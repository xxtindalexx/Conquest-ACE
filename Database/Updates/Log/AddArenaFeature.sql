USE `ace_log`;

ALTER TABLE pk_kills_log
ADD killer_arena_player_id INT;

ALTER TABLE pk_kills_log
ADD victim_arena_player_id INT;

--
-- Table structure for table `arena_event
--

DROP TABLE IF EXISTS `arena_event`;
CREATE TABLE `arena_event` (
  `id` INT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT 'Unique Id of the arena event',  
  `event_type` VARCHAR(16),
  `location` INT UNSIGNED,
  `status` INT,
  `start_datetime` DATETIME,
  `end_datetime` DATETIME,
  `winning_team_guid` VARCHAR(36),
  `cancel_reason` VARCHAR(500),
  `is_overtime` BIT NOT NULL DEFAULT(0),
  `create_datetime` DATETIME,
  PRIMARY KEY (`id`)
) ENGINE=INNODB DEFAULT CHARSET=utf8mb4;

--
-- Table structure for table `arena_player`
--

DROP TABLE IF EXISTS `arena_player`;
CREATE TABLE `arena_player` (
  `id` INT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT 'Unique Id of the arena player instance',  
  `character_id` INT UNSIGNED,
  `character_name` VARCHAR(255),
  `character_level` INT UNSIGNED,
  `event_type`  VARCHAR(16),  
  `monarch_id` INT UNSIGNED,
  `monarch_name` VARCHAR(255), 
  `event_id` INT UNSIGNED,
  `team_guid` CHAR(36),
  `is_eliminated` BIT,
  `finish_place` INT,
  `total_deaths` INT UNSIGNED,
  `total_kills` INT UNSIGNED,
  `total_dmg_dealt` INT UNSIGNED,
  `total_dmg_received` INT UNSIGNED,
  `create_datetime` DATETIME,
  `player_ip` VARCHAR(25),  
  PRIMARY KEY (`id`)
) ENGINE=INNODB DEFAULT CHARSET=utf8mb4;

--
-- Table structure for table `arena_character_stats`
--

DROP TABLE IF EXISTS `arena_character_stats`;
CREATE TABLE `arena_character_stats` (
  `id` INT UNSIGNED NOT NULL AUTO_INCREMENT COMMENT 'Unique Id of the arena stats instance',  
  `character_id` INT UNSIGNED,
  `character_name` VARCHAR(255),
  `event_type` VARCHAR(12),
  `total_matches` INT UNSIGNED,
  `total_wins` INT UNSIGNED,
  `total_losses` INT UNSIGNED,
  `total_draws` INT UNSIGNED,
  `total_disqualified` INT UNSIGNED,  
  `total_deaths` INT UNSIGNED,
  `total_kills` INT UNSIGNED,
  `total_dmg_dealt` INT UNSIGNED,
  `total_dmg_received` INT UNSIGNED,
  `rank_points` INT UNSIGNED,
  PRIMARY KEY (`id`)
) ENGINE=INNODB DEFAULT CHARSET=utf8mb4;

