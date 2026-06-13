using Discord.WebSocket;
using DonBot.Core.Models.Entities;
using DonBot.Core.Models.Enums;

namespace DonBot.Services.SchedulerServices;

public interface IScheduledEventHandler
{
    ScheduledEventTypeEnum EventType { get; }

    Task HandleAsync(ScheduledEvent scheduledEvent, SocketGuild socketGuild);
}
