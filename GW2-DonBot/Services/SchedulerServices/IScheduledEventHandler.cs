using Discord.WebSocket;
using DonBot.Models.Entities;
using DonBot.Models.Enums;

namespace DonBot.Services.SchedulerServices;

public interface IScheduledEventHandler
{
    ScheduledEventTypeEnum EventType { get; }

    Task HandleAsync(ScheduledEvent scheduledEvent, SocketGuild socketGuild);
}
