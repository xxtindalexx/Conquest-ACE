-- CONQUEST: Luminance Lottery
-- Lottery entries are stored in the existing biota_properties_int64 table using
-- two custom PropertyInt64 values (type 9062 = LotteryTickets, type 9063 = LotteryWeekNumber).
-- No new table is required.  This index improves draw-time lookup performance.

ALTER TABLE `biota_properties_int64`
  ADD INDEX IF NOT EXISTS `idx_biota_prop_int64_type` (`type`);
