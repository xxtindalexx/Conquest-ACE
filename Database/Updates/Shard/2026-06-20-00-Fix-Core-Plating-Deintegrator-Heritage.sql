/* PropertyInt.HeritageSpecificArmor (324) - remove Invalid (0) values left by recipe-based deintegration */

DELETE FROM biota_properties_int WHERE `type` = 324 AND `value` = 0;
