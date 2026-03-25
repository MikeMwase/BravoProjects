-- Tell EF that these two migrations are already finished
INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES ('20260224101118_MonthOnEnrollments', '9.0.13');

-- Note: We saw in your logs that '20260219122101_DailyExpenses' 
-- was applied successfully right before the crash, so we don't need to add it.