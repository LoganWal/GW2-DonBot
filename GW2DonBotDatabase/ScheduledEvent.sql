CREATE TABLE [dbo].[ScheduledEvent]
(
    [ScheduledEventId] BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [GuildId] BIGINT NOT NULL,
    [Message] [nvarchar](256) NOT NULL,
    [MessageId] BIGINT NULL,
    [ChannelId] BIGINT NOT NULL,
    [Day] SMALLINT NOT NULL,
    [Hour] SMALLINT NOT NULL,
    [UtcEventTime] DATETIME2 NOT NULL,
    [EventType] SMALLINT NOT NULL DEFAULT 0,
    [RoleId] BIGINT NULL,
    [RepeatIntervalDays] SMALLINT NOT NULL DEFAULT 7,
    FOREIGN KEY (GuildId) REFERENCES Guild(GuildId)
);