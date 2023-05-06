CREATE TABLE PlayerRaffleBid (
    RaffleId int NOT NULL,
    DiscordId bigint NOT NULL,
    PointsSpent decimal(16, 3) NOT NULL,
    CONSTRAINT PK_PlayerRaffleBid PRIMARY KEY (raffleId, discordId),
    FOREIGN KEY (RaffleId) REFERENCES Raffle(id),
    FOREIGN KEY (DiscordId) REFERENCES Account(DiscordId)
);