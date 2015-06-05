CREATE DATABASE  IF NOT EXISTS `dev_hybrasyl` /*!40100 DEFAULT CHARACTER SET utf8 */;
USE `dev_hybrasyl`;
-- MySQL dump 10.13  Distrib 5.6.17, for Win32 (x86)
--
-- Host: localhost    Database: dev_hybrasyl
-- ------------------------------------------------------
-- Server version	5.6.19-log

/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!40101 SET NAMES utf8 */;
/*!40103 SET @OLD_TIME_ZONE=@@TIME_ZONE */;
/*!40103 SET TIME_ZONE='+00:00' */;
/*!40014 SET @OLD_UNIQUE_CHECKS=@@UNIQUE_CHECKS, UNIQUE_CHECKS=0 */;
/*!40014 SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0 */;
/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;
/*!40111 SET @OLD_SQL_NOTES=@@SQL_NOTES, SQL_NOTES=0 */;

--
-- Table structure for table `accounts`
--

DROP TABLE IF EXISTS `accounts`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `accounts` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `nickname` varchar(255) NOT NULL,
  `enabled` tinyint(1) NOT NULL DEFAULT '1',
  `email` varchar(255) NOT NULL DEFAULT '',
  `encrypted_password` varchar(255) NOT NULL DEFAULT '',
  `reset_password_token` varchar(255) DEFAULT NULL,
  `reset_password_sent_at` datetime DEFAULT NULL,
  `remember_created_at` datetime DEFAULT NULL,
  `sign_in_count` int(11) DEFAULT '0',
  `current_sign_in_at` datetime DEFAULT NULL,
  `last_sign_in_at` datetime DEFAULT NULL,
  `current_sign_in_ip` varchar(255) DEFAULT NULL,
  `last_sign_in_ip` varchar(255) DEFAULT NULL,
  `confirmation_token` varchar(255) DEFAULT NULL,
  `confirmed_at` datetime DEFAULT NULL,
  `confirmation_sent_at` datetime DEFAULT NULL,
  `unconfirmed_email` varchar(255) DEFAULT NULL,
  `failed_attempts` int(11) DEFAULT '0',
  `unlock_token` varchar(255) DEFAULT NULL,
  `locked_at` datetime DEFAULT NULL,
  `authentication_token` varchar(255) DEFAULT NULL,
  `created_at` datetime DEFAULT NULL,
  `updated_at` datetime DEFAULT NULL,
  `roles_mask` int(11) DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `index_accounts_on_email` (`email`),
  UNIQUE KEY `index_accounts_on_reset_password_token` (`reset_password_token`),
  UNIQUE KEY `index_accounts_on_confirmation_token` (`confirmation_token`),
  UNIQUE KEY `index_accounts_on_unlock_token` (`unlock_token`),
  UNIQUE KEY `index_accounts_on_authentication_token` (`authentication_token`)
) ENGINE=InnoDB AUTO_INCREMENT=350 DEFAULT CHARSET=latin1;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `active_admin_comments`
--

DROP TABLE IF EXISTS `active_admin_comments`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `active_admin_comments` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `resource_id` varchar(255) NOT NULL,
  `resource_type` varchar(255) NOT NULL,
  `author_id` int(11) DEFAULT NULL,
  `author_type` varchar(255) DEFAULT NULL,
  `body` text,
  `created_at` datetime NOT NULL,
  `updated_at` datetime NOT NULL,
  `namespace` varchar(255) DEFAULT NULL,
  PRIMARY KEY (`id`),
  KEY `index_admin_notes_on_resource_type_and_resource_id` (`resource_type`,`resource_id`),
  KEY `index_active_admin_comments_on_namespace` (`namespace`),
  KEY `index_active_admin_comments_on_author_type_and_author_id` (`author_type`,`author_id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `api_keys`
--

DROP TABLE IF EXISTS `api_keys`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `api_keys` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `access_token` varchar(255) DEFAULT NULL,
  `created_at` datetime NOT NULL,
  `updated_at` datetime NOT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `board_messages`
--

DROP TABLE IF EXISTS `board_messages`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `board_messages` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `title` varchar(255) NOT NULL,
  `message` text NOT NULL,
  `sender_id` int(11) NOT NULL,
  `created_at` datetime NOT NULL,
  `updated_at` datetime NOT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `boards`
--

DROP TABLE IF EXISTS `boards`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `boards` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `name` varchar(255) NOT NULL,
  `created_at` datetime NOT NULL,
  `updated_at` datetime NOT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `drop_sets`
--

DROP TABLE IF EXISTS `drop_sets`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `drop_sets` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `name` varchar(255) NOT NULL,
  `enabled` tinyint(1) NOT NULL DEFAULT '1',
  `created_at` datetime NOT NULL,
  `updated_at` datetime NOT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `drop_sets_drops`
--

DROP TABLE IF EXISTS `drop_sets_drops`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `drop_sets_drops` (
  `drop_id` int(11) NOT NULL DEFAULT '0',
  `drop_set_id` int(11) NOT NULL DEFAULT '0',
  PRIMARY KEY (`drop_id`,`drop_set_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `drop_sets_mobs`
--

DROP TABLE IF EXISTS `drop_sets_mobs`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `drop_sets_mobs` (
  `drop_set_id` int(11) NOT NULL DEFAULT '0',
  `mob_id` int(11) NOT NULL DEFAULT '0',
  PRIMARY KEY (`mob_id`,`drop_set_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `drops`
--

DROP TABLE IF EXISTS `drops`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `drops` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `chance` decimal(10,8) NOT NULL DEFAULT '1.00000000',
  `item_id` int(11) NOT NULL,
  `min_quantity` int(11) NOT NULL DEFAULT '0',
  `max_quantity` int(11) NOT NULL DEFAULT '1',
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `drops_item_variants`
--

DROP TABLE IF EXISTS `drops_item_variants`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `drops_item_variants` (
  `drop_id` int(11) NOT NULL,
  `item_variant_id` int(11) NOT NULL,
  PRIMARY KEY (`drop_id`,`item_variant_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `drops_mobs`
--

DROP TABLE IF EXISTS `drops_mobs`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `drops_mobs` (
  `drop_id` int(11) NOT NULL DEFAULT '0',
  `mob_id` int(11) NOT NULL DEFAULT '0',
  PRIMARY KEY (`mob_id`,`drop_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `flags`
--

DROP TABLE IF EXISTS `flags`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `flags` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `name` varchar(255) NOT NULL,
  `description` varchar(255) NOT NULL,
  `created_at` datetime NOT NULL,
  `updated_at` datetime NOT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=8 DEFAULT CHARSET=latin1;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `flags_players`
--

DROP TABLE IF EXISTS `flags_players`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `flags_players` (
  `player_id` int(11) NOT NULL,
  `flag_id` int(11) DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=latin1;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `item_variants`
--

DROP TABLE IF EXISTS `item_variants`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `item_variants` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `name` varchar(255) NOT NULL,
  `modifier` varchar(255) NOT NULL,
  `effect_script_name` varchar(255) DEFAULT NULL,
  `weight` varchar(255) DEFAULT NULL,
  `max_stack` varchar(255) DEFAULT NULL,
  `max_durability` varchar(255) DEFAULT NULL,
  `hp` varchar(255) DEFAULT NULL,
  `mp` varchar(255) DEFAULT NULL,
  `str` varchar(255) DEFAULT NULL,
  `int` varchar(255) DEFAULT NULL,
  `wis` varchar(255) DEFAULT NULL,
  `con` varchar(255) DEFAULT NULL,
  `dex` varchar(255) DEFAULT NULL,
  `hit` varchar(255) DEFAULT NULL,
  `ac` varchar(255) DEFAULT NULL,
  `dmg` varchar(255) DEFAULT NULL,
  `mr` varchar(255) DEFAULT NULL,
  `max_s_dmg` varchar(255) DEFAULT NULL,
  `min_s_dmg` varchar(255) DEFAULT NULL,
  `max_l_dmg` varchar(255) DEFAULT NULL,
  `min_l_dmg` varchar(255) DEFAULT NULL,
  `value` varchar(255) DEFAULT NULL,
  `regen` varchar(255) DEFAULT NULL,
  `level` int(11) DEFAULT NULL,
  `ab` int(11) DEFAULT NULL,
  `element` int(11) DEFAULT NULL,
  `bodystyle` int(11) NOT NULL DEFAULT '1',
  `color` int(11) DEFAULT NULL,
  `enchantable` tinyint(1) DEFAULT '0',
  `depositable` tinyint(1) DEFAULT '1',
  `bound` tinyint(1) DEFAULT '0',
  `vendorable` tinyint(1) DEFAULT '1',
  `tailorable` tinyint(1) DEFAULT '0',
  `smithable` tinyint(1) DEFAULT '0',
  `consecratable` tinyint(1) DEFAULT '0',
  `perishable` tinyint(1) DEFAULT '1',
  `exchangeable` tinyint(1) DEFAULT '1',
  `consecratable_variant` tinyint(1) DEFAULT '0',
  `tailorable_variant` tinyint(1) DEFAULT '0',
  `smithable_variant` tinyint(1) DEFAULT '0',
  `enchantable_variant` tinyint(1) DEFAULT '0',
  `elemental_variant` tinyint(1) DEFAULT '0',
  `created_at` datetime NOT NULL,
  `updated_at` datetime NOT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=31 DEFAULT CHARSET=latin1;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `item_variants_items`
--

DROP TABLE IF EXISTS `item_variants_items`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `item_variants_items` (
  `item_id` int(11) NOT NULL,
  `item_variant_id` int(11) NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=latin1;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `items`
--

DROP TABLE IF EXISTS `items`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `items` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `name` varchar(255) NOT NULL,
  `sprite` int(11) NOT NULL,
  `equip_sprite` int(11) NOT NULL DEFAULT '-1',
  `display_sprite` int(11) DEFAULT NULL,
  `bodystyle` int(11) NOT NULL DEFAULT '0',
  `item_type` int(11) NOT NULL,
  `weapon_type` int(11) DEFAULT NULL,
  `equip_slot` int(11) DEFAULT NULL,
  `weight` int(11) NOT NULL DEFAULT '0',
  `max_stack` int(11) NOT NULL DEFAULT '1',
  `max_durability` int(11) NOT NULL DEFAULT '0',
  `level` int(11) NOT NULL DEFAULT '0',
  `ab` int(11) NOT NULL DEFAULT '0',
  `class_type` int(11) DEFAULT '0',
  `sex` int(11) NOT NULL DEFAULT '0',
  `hp` int(11) NOT NULL DEFAULT '0',
  `mp` int(11) NOT NULL DEFAULT '0',
  `str` int(11) NOT NULL DEFAULT '0',
  `int` int(11) NOT NULL DEFAULT '0',
  `wis` int(11) NOT NULL DEFAULT '0',
  `con` int(11) NOT NULL DEFAULT '0',
  `dex` int(11) NOT NULL DEFAULT '0',
  `hit` int(11) NOT NULL DEFAULT '0',
  `dmg` int(11) NOT NULL DEFAULT '0',
  `ac` int(11) NOT NULL DEFAULT '0',
  `mr` int(11) NOT NULL DEFAULT '0',
  `element` int(11) NOT NULL DEFAULT '0',
  `max_s_dmg` int(11) NOT NULL DEFAULT '0',
  `min_s_dmg` int(11) NOT NULL DEFAULT '0',
  `max_l_dmg` int(11) NOT NULL DEFAULT '0',
  `min_l_dmg` int(11) NOT NULL DEFAULT '0',
  `value` int(11) NOT NULL DEFAULT '0',
  `color` int(11) NOT NULL DEFAULT '0',
  `regen` int(11) NOT NULL DEFAULT '0',
  `bound` tinyint(1) NOT NULL DEFAULT '0',
  `depositable` tinyint(1) NOT NULL DEFAULT '1',
  `enchantable` tinyint(1) NOT NULL DEFAULT '0',
  `consecratable` tinyint(1) NOT NULL DEFAULT '0',
  `tailorable` tinyint(1) NOT NULL DEFAULT '0',
  `smithable` tinyint(1) NOT NULL DEFAULT '0',
  `exchangeable` tinyint(1) NOT NULL DEFAULT '1',
  `has_enchantable_variants` tinyint(1) NOT NULL DEFAULT '0',
  `has_consecratable_variants` tinyint(1) NOT NULL DEFAULT '0',
  `has_tailorable_variants` tinyint(1) NOT NULL DEFAULT '0',
  `has_smithable_variants` tinyint(1) NOT NULL DEFAULT '0',
  `has_elemental_variants` tinyint(1) NOT NULL DEFAULT '0',
  `created_at` datetime NOT NULL,
  `updated_at` datetime NOT NULL,
  `vendorable` tinyint(1) DEFAULT '1',
  `script_name` varchar(255) DEFAULT NULL,
  `perishable` tinyint(1) DEFAULT '0',
  `max_dmg` int(11) DEFAULT NULL,
  `shoptab` varchar(8) DEFAULT NULL,
  `shopdesc` varchar(254) DEFAULT NULL,
  `master_only` tinyint(1) NOT NULL DEFAULT '0',
  `unique_equipped` tinyint(1) NOT NULL DEFAULT '0',
  `unique` tinyint(1) NOT NULL DEFAULT '0',
  `consumed_on_use` tinyint(1) NOT NULL DEFAULT '1',
  `teleport_destination` int(11) DEFAULT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=1406 DEFAULT CHARSET=latin1;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `items_npcs`
--

DROP TABLE IF EXISTS `items_npcs`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `items_npcs` (
  `item_id` int(11) NOT NULL,
  `npc_id` int(11) NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `legend_marks`
--

DROP TABLE IF EXISTS `legend_marks`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `legend_marks` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `player_id` int(11) NOT NULL,
  `prefix` varchar(255) NOT NULL,
  `color` int(11) NOT NULL DEFAULT '16',
  `icon` int(11) NOT NULL DEFAULT '0',
  `text` varchar(255) NOT NULL,
  `public` tinyint(1) NOT NULL DEFAULT '1',
  `created_at` datetime NOT NULL,
  `updated_at` datetime NOT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=51 DEFAULT CHARSET=latin1;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `maps`
--

DROP TABLE IF EXISTS `maps`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `maps` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `size_x` int(11) NOT NULL,
  `size_y` int(11) NOT NULL,
  `name` varchar(255) NOT NULL,
  `flags` int(11) NOT NULL DEFAULT '0',
  `created_at` datetime NOT NULL,
  `updated_at` datetime NOT NULL,
  `music` int(11) DEFAULT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=10057 DEFAULT CHARSET=latin1;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `messages`
--

DROP TABLE IF EXISTS `messages`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `messages` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `index` int(11) NOT NULL DEFAULT '0',
  `title` varchar(255) NOT NULL,
  `message` text NOT NULL,
  `recipient_id` int(11) NOT NULL,
  `sender_id` int(11) NOT NULL,
  `created_at` datetime NOT NULL,
  `updated_at` datetime NOT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `mobs`
--

DROP TABLE IF EXISTS `mobs`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `mobs` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `name` varchar(255) NOT NULL,
  `sprite` int(11) NOT NULL,
  `created_at` datetime NOT NULL,
  `updated_at` datetime NOT NULL,
  `level` int(11) NOT NULL DEFAULT '1',
  `min_dmg` int(11) NOT NULL DEFAULT '1',
  `max_dmg` int(11) NOT NULL DEFAULT '1',
  `mr` int(11) NOT NULL DEFAULT '0',
  `ac` int(11) NOT NULL DEFAULT '100',
  `force_multiplier` float NOT NULL DEFAULT '1',
  `off_element` int(11) NOT NULL DEFAULT '0',
  `def_element` int(11) NOT NULL DEFAULT '0',
  `exp` int(11) NOT NULL DEFAULT '0',
  `gold` int(11) NOT NULL DEFAULT '0',
  `description` varchar(255) DEFAULT NULL,
  `script_name` varchar(255) DEFAULT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `nations`
--

DROP TABLE IF EXISTS `nations`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `nations` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `flag` int(11) NOT NULL,
  `name` varchar(255) NOT NULL,
  `description` text,
  `created_at` datetime NOT NULL,
  `updated_at` datetime NOT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=7 DEFAULT CHARSET=latin1;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `news_posts`
--

DROP TABLE IF EXISTS `news_posts`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `news_posts` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `account_id` int(11) NOT NULL,
  `title` varchar(255) NOT NULL,
  `post` text NOT NULL,
  `created_at` datetime NOT NULL,
  `updated_at` datetime NOT NULL,
  `post_date` datetime DEFAULT NULL,
  PRIMARY KEY (`id`),
  KEY `index_news_posts_on_account_id` (`account_id`)
) ENGINE=InnoDB AUTO_INCREMENT=21 DEFAULT CHARSET=latin1;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `npcs`
--

DROP TABLE IF EXISTS `npcs`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `npcs` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `name` varchar(255) NOT NULL,
  `sprite` int(11) NOT NULL,
  `created_at` datetime NOT NULL,
  `updated_at` datetime NOT NULL,
  `map_id` int(11) NOT NULL,
  `map_x` int(11) NOT NULL,
  `map_y` int(11) NOT NULL,
  `direction` int(11) NOT NULL DEFAULT '0',
  `display_text` text,
  `jobs` int(11) NOT NULL DEFAULT '0',
  `portrait` varchar(255) DEFAULT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=243 DEFAULT CHARSET=latin1;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `players`
--

DROP TABLE IF EXISTS `players`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `players` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `account_id` int(11) DEFAULT NULL,
  `name` varchar(255) NOT NULL,
  `password_hash` varchar(255) DEFAULT NULL,
  `sex` int(11) NOT NULL,
  `hairstyle` int(11) NOT NULL,
  `haircolor` int(11) NOT NULL,
  `map_id` int(11) DEFAULT NULL,
  `map_x` int(11) DEFAULT NULL,
  `map_y` int(11) DEFAULT NULL,
  `direction` int(11) NOT NULL DEFAULT '0',
  `class_type` int(11) NOT NULL DEFAULT '0',
  `level` int(11) NOT NULL DEFAULT '1',
  `exp` int(11) NOT NULL DEFAULT '0',
  `ab` int(11) NOT NULL DEFAULT '0',
  `ab_exp` int(11) NOT NULL DEFAULT '0',
  `max_hp` int(11) NOT NULL DEFAULT '50',
  `max_mp` int(11) NOT NULL DEFAULT '50',
  `cur_hp` int(11) NOT NULL DEFAULT '50',
  `cur_mp` int(11) NOT NULL DEFAULT '50',
  `str` int(11) NOT NULL DEFAULT '3',
  `int` int(11) NOT NULL DEFAULT '3',
  `wis` int(11) NOT NULL DEFAULT '3',
  `con` int(11) NOT NULL DEFAULT '3',
  `dex` int(11) NOT NULL DEFAULT '3',
  `equipment` varchar(8192) NOT NULL DEFAULT '[]',
  `inventory` varchar(8192) NOT NULL DEFAULT '[]',
  `created_at` datetime NOT NULL,
  `updated_at` datetime NOT NULL,
  `nation_id` int(11) DEFAULT NULL,
  `last_login` datetime DEFAULT NULL,
  `last_logoff` datetime DEFAULT NULL,
  `gold` int(11) NOT NULL DEFAULT '0',
  `level_points` int(11) NOT NULL DEFAULT '0',
  PRIMARY KEY (`id`),
  KEY `index_players_on_account_id` (`account_id`),
  KEY `index_players_on_map_id` (`map_id`)
) ENGINE=InnoDB AUTO_INCREMENT=446 DEFAULT CHARSET=latin1;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `reactors`
--

DROP TABLE IF EXISTS `reactors`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `reactors` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `name` varchar(255) NOT NULL,
  `created_at` datetime NOT NULL,
  `updated_at` datetime NOT NULL,
  `map_id` int(11) NOT NULL,
  `map_x` int(11) NOT NULL,
  `map_y` int(11) NOT NULL,
  `script_name` varchar(255) DEFAULT NULL,
  `blocking` tinyint(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `schema_migrations`
--

DROP TABLE IF EXISTS `schema_migrations`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `schema_migrations` (
  `version` varchar(255) NOT NULL,
  UNIQUE KEY `unique_schema_migrations` (`version`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `signposts`
--

DROP TABLE IF EXISTS `signposts`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `signposts` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `map_id` int(11) NOT NULL,
  `map_x` int(11) NOT NULL,
  `map_y` int(11) NOT NULL,
  `message` text NOT NULL,
  `is_messageboard` tinyint(1) NOT NULL DEFAULT '0',
  `board_id` int(11) DEFAULT NULL,
  `created_at` datetime NOT NULL,
  `updated_at` datetime NOT NULL,
  PRIMARY KEY (`id`),
  KEY `index_signposts_on_map_id` (`map_id`),
  KEY `index_signposts_on_board_id` (`board_id`)
) ENGINE=InnoDB AUTO_INCREMENT=269 DEFAULT CHARSET=latin1;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `skills`
--

DROP TABLE IF EXISTS `skills`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `skills` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `name` varchar(255) NOT NULL,
  `sprite` int(11) NOT NULL,
  `created_at` datetime NOT NULL,
  `updated_at` datetime NOT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `spawn_points`
--

DROP TABLE IF EXISTS `spawn_points`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `spawn_points` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `map_id` int(11) NOT NULL,
  `map_x` int(11) NOT NULL,
  `map_y` int(11) NOT NULL,
  `created_at` datetime NOT NULL,
  `updated_at` datetime NOT NULL,
  `nation_id` int(11) NOT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=2 DEFAULT CHARSET=latin1;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `spawns`
--

DROP TABLE IF EXISTS `spawns`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `spawns` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `mob_id` int(11) NOT NULL,
  `map_id` int(11) NOT NULL,
  `quantity` int(11) NOT NULL DEFAULT '0',
  `ticks` int(11) NOT NULL DEFAULT '1',
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `spells`
--

DROP TABLE IF EXISTS `spells`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `spells` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `name` varchar(255) NOT NULL,
  `sprite` int(11) NOT NULL,
  `created_at` datetime NOT NULL,
  `updated_at` datetime NOT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `taggings`
--

DROP TABLE IF EXISTS `taggings`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `taggings` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `tag_id` int(11) DEFAULT NULL,
  `taggable_id` int(11) DEFAULT NULL,
  `taggable_type` varchar(255) DEFAULT NULL,
  `tagger_id` int(11) DEFAULT NULL,
  `tagger_type` varchar(255) DEFAULT NULL,
  `context` varchar(128) DEFAULT NULL,
  `created_at` datetime DEFAULT NULL,
  PRIMARY KEY (`id`),
  KEY `index_taggings_on_tag_id` (`tag_id`),
  KEY `index_taggings_on_taggable_id_and_taggable_type_and_context` (`taggable_id`,`taggable_type`,`context`)
) ENGINE=InnoDB AUTO_INCREMENT=40 DEFAULT CHARSET=latin1;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `tags`
--

DROP TABLE IF EXISTS `tags`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `tags` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `name` varchar(255) DEFAULT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=3 DEFAULT CHARSET=latin1;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `vendor_items`
--

DROP TABLE IF EXISTS `vendor_items`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `vendor_items` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `npc_id` int(11) NOT NULL,
  `item_id` int(11) NOT NULL,
  PRIMARY KEY (`id`),
  KEY `index_vendor_items_on_npc_id` (`npc_id`),
  KEY `index_vendor_items_on_item_id` (`item_id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `warps`
--

DROP TABLE IF EXISTS `warps`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `warps` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `source_x` varchar(255) NOT NULL,
  `source_y` varchar(255) NOT NULL,
  `target_x` varchar(255) NOT NULL,
  `target_y` varchar(255) NOT NULL,
  `max_lev` int(11) NOT NULL,
  `min_lev` int(11) NOT NULL,
  `min_ab` int(11) NOT NULL,
  `mob_use` tinyint(1) NOT NULL DEFAULT '0',
  `source_id` int(11) NOT NULL,
  `target_id` int(11) NOT NULL,
  `created_at` datetime NOT NULL,
  `updated_at` datetime NOT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=4797 DEFAULT CHARSET=latin1;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `worldmap_points`
--

DROP TABLE IF EXISTS `worldmap_points`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `worldmap_points` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `name` varchar(255) NOT NULL,
  `worldmap_id` int(11) NOT NULL,
  `map_x` int(11) NOT NULL,
  `map_y` int(11) NOT NULL,
  `target_map_id` int(11) NOT NULL,
  `target_x` int(11) NOT NULL,
  `target_y` int(11) NOT NULL,
  `min_lev` int(11) NOT NULL DEFAULT '1',
  `max_lev` int(11) NOT NULL DEFAULT '99',
  `min_ab` int(11) NOT NULL DEFAULT '0',
  `created_at` datetime NOT NULL,
  `updated_at` datetime NOT NULL,
  PRIMARY KEY (`id`),
  KEY `index_worldmap_points_on_worldmap_id` (`worldmap_id`),
  KEY `index_worldmap_points_on_target_map_id` (`target_map_id`)
) ENGINE=InnoDB AUTO_INCREMENT=68 DEFAULT CHARSET=latin1;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `worldmaps`
--

DROP TABLE IF EXISTS `worldmaps`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `worldmaps` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `name` varchar(255) NOT NULL,
  `client_map` varchar(255) NOT NULL,
  `created_at` datetime NOT NULL,
  `updated_at` datetime NOT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=11 DEFAULT CHARSET=latin1;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `worldwarps`
--

DROP TABLE IF EXISTS `worldwarps`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `worldwarps` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `target_worldmap_id` int(11) NOT NULL,
  `min_lev` int(11) NOT NULL,
  `max_lev` int(11) NOT NULL,
  `min_ab` int(11) NOT NULL,
  `source_x` varchar(255) NOT NULL,
  `source_y` varchar(255) NOT NULL,
  `source_map_id` int(11) NOT NULL,
  `created_at` datetime NOT NULL,
  `updated_at` datetime NOT NULL,
  PRIMARY KEY (`id`),
  KEY `index_worldwarps_on_target_worldmap_id` (`target_worldmap_id`),
  KEY `index_worldwarps_on_source_map_id` (`source_map_id`)
) ENGINE=InnoDB AUTO_INCREMENT=126 DEFAULT CHARSET=latin1;
/*!40101 SET character_set_client = @saved_cs_client */;
/*!40103 SET TIME_ZONE=@OLD_TIME_ZONE */;

/*!40101 SET SQL_MODE=@OLD_SQL_MODE */;
/*!40014 SET FOREIGN_KEY_CHECKS=@OLD_FOREIGN_KEY_CHECKS */;
/*!40014 SET UNIQUE_CHECKS=@OLD_UNIQUE_CHECKS */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
/*!40111 SET SQL_NOTES=@OLD_SQL_NOTES */;

-- Dump completed on 2015-06-05 10:03:33
