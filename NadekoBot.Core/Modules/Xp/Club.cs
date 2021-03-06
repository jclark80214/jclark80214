using Discord;
using Discord.Commands;
using NadekoBot.Common.Attributes;
using NadekoBot.Core.Services.Database.Models;
using NadekoBot.Extensions;
using NadekoBot.Modules.Xp.Common;
using NadekoBot.Modules.Xp.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace NadekoBot.Modules.Xp
{
    public partial class Xp
    {
        [Group]
        public class Club : NadekoSubmodule<ClubService>
        {
            private readonly XpService _xps;

            public Club(XpService xps)
            {
                _xps = xps;
            }

            [NadekoCommand, Usage, Description, Aliases]
            public async Task ClubTransfer([Remainder]IUser newOwner)
            {
                var club = _service.TransferClub(Context.User, newOwner);

                if (club != null)
                    await ReplyConfirmLocalized("club_transfered",
                        Format.Bold(club.Name),
                        Format.Bold(newOwner.ToString())).ConfigureAwait(false);
                else
                    await ReplyErrorLocalized("club_transfer_failed").ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            public async Task ClubAdmin([Remainder]IUser toAdmin)
            {
                bool admin;
                try
                {
                    admin = _service.ToggleAdmin(Context.User, toAdmin);
                }
                catch (InvalidOperationException)
                {
                    await ReplyErrorLocalized("club_admin_error").ConfigureAwait(false);
                    return;
                }

                if (admin)
                    await ReplyConfirmLocalized("club_admin_add", Format.Bold(toAdmin.ToString())).ConfigureAwait(false);
                else
                    await ReplyConfirmLocalized("club_admin_remove", Format.Bold(toAdmin.ToString())).ConfigureAwait(false);

            }

            [NadekoCommand, Usage, Description, Aliases]
            public async Task ClubCreate([Remainder]string clubName)
            {
                if (string.IsNullOrWhiteSpace(clubName) || clubName.Length > 20)
                    return;

                if (!_service.CreateClub(Context.User, clubName, out ClubInfo club))
                {
                    await ReplyErrorLocalized("club_create_error").ConfigureAwait(false);
                    return;
                }

                await ReplyConfirmLocalized("club_created", Format.Bold(club.ToString())).ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            public async Task ClubIcon([Remainder]string url = null)
            {
                if ((!Uri.IsWellFormedUriString(url, UriKind.Absolute) && url != null)
                    || !await _service.SetClubIcon(Context.User.Id, url == null ? null : new Uri(url)))
                {
                    await ReplyErrorLocalized("club_icon_error").ConfigureAwait(false);
                    return;
                }

                await ReplyConfirmLocalized("club_icon_set").ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [Priority(1)]
            public async Task ClubInformation(IUser user = null)
            {
                user = user ?? Context.User;
                var club = _service.GetClubByMember(user);
                if (club == null)
                {
                    await ReplyErrorLocalized("club_not_exists").ConfigureAwait(false);
                    return;
                }

                await ClubInformation(club.ToString()).ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [Priority(0)]
            public async Task ClubInformation([Remainder]string clubName = null)
            {
                if (string.IsNullOrWhiteSpace(clubName))
                {
                    await ClubInformation(Context.User).ConfigureAwait(false);
                    return;
                }

                if (!_service.GetClubByName(clubName, out ClubInfo club))
                {
                    await ReplyErrorLocalized("club_not_exists").ConfigureAwait(false);
                    return;
                }

                var lvl = new LevelStats(club.Xp);
                var users = club.Users
                    .OrderByDescending(x =>
                    {
                        var l = new LevelStats(x.TotalXp).Level;
                        if (club.OwnerId == x.Id)
                            return int.MaxValue;
                        else if (x.IsClubAdmin)
                            return int.MaxValue / 2 + l;
                        else
                            return l;
                    });

                await Context.SendPaginatedConfirmAsync(0, (page) =>
                {
                    var embed = new EmbedBuilder()
                        .WithOkColor()
                        .WithTitle($"{club.ToString()}")
                        .WithDescription(GetText("level_x", lvl.Level) + $" ({club.Xp} xp)")
                        .AddField("Description", string.IsNullOrWhiteSpace(club.Description) ? "-" : club.Description, false)
                        .AddField("Owner", club.Owner.ToString(), true)
                        .AddField("Level Req.", club.MinimumLevelReq.ToString(), true)
                        .AddField("Members", string.Join("\n", users
                            .Skip(page * 10)
                            .Take(10)
                            .Select(x =>
                            {
                                var l = new LevelStats(x.TotalXp);
                                var lvlStr = Format.Bold($" ⟪{l.Level}⟫");
                                if (club.OwnerId == x.Id)
                                    return x.ToString() + "🌟" + lvlStr;
                                else if (x.IsClubAdmin)
                                    return x.ToString() + "⭐" + lvlStr;
                                return x.ToString() + lvlStr;
                            })), false);

                    if (Uri.IsWellFormedUriString(club.ImageUrl, UriKind.Absolute))
                        return embed.WithThumbnailUrl(club.ImageUrl);

                    return embed;
                }, club.Users.Count, 10).ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            public Task ClubBans(int page = 1)
            {
                if (--page < 0)
                    return Task.CompletedTask;

                var club = _service.GetClubWithBansAndApplications(Context.User.Id);
                if (club == null)
                    return ReplyErrorLocalized("club_not_exists_owner");

                var bans = club
                    .Bans
                    .Select(x => x.User)
                    .ToArray();

                return Context.SendPaginatedConfirmAsync(page,
                    curPage =>
                    {
                        var toShow = string.Join("\n", bans
                            .Skip(page * 10)
                            .Take(10)
                            .Select(x => x.ToString()));

                        return new EmbedBuilder()
                            .WithTitle(GetText("club_bans_for", club.ToString()))
                            .WithDescription(toShow)
                            .WithOkColor();

                    }, bans.Length, 10);
            }


            [NadekoCommand, Usage, Description, Aliases]
            public Task ClubApps(int page = 1)
            {
                if (--page < 0)
                    return Task.CompletedTask;

                var club = _service.GetClubWithBansAndApplications(Context.User.Id);
                if (club == null)
                    return ReplyErrorLocalized("club_not_exists_owner");

                var apps = club
                    .Applicants
                    .Select(x => x.User)
                    .ToArray();

                return Context.SendPaginatedConfirmAsync(page,
                    curPage =>
                    {
                        var toShow = string.Join("\n", apps
                            .Skip(page * 10)
                            .Take(10)
                            .Select(x => x.ToString()));

                        return new EmbedBuilder()
                            .WithTitle(GetText("club_apps_for", club.ToString()))
                            .WithDescription(toShow)
                            .WithOkColor();

                    }, apps.Length, 10);
            }

            [NadekoCommand, Usage, Description, Aliases]
            public async Task ClubApply([Remainder]string clubName)
            {
                if (string.IsNullOrWhiteSpace(clubName))
                    return;

                if (!_service.GetClubByName(clubName, out ClubInfo club))
                {
                    await ReplyErrorLocalized("club_not_exists").ConfigureAwait(false);
                    return;
                }

                if (_service.ApplyToClub(Context.User, club))
                {
                    await ReplyConfirmLocalized("club_applied", Format.Bold(club.ToString())).ConfigureAwait(false);
                }
                else
                {
                    await ReplyErrorLocalized("club_apply_error").ConfigureAwait(false);
                }
            }

            [NadekoCommand, Usage, Description, Aliases]
            [Priority(1)]
            public Task ClubAccept(IUser user)
                => ClubAccept(user.ToString());

            [NadekoCommand, Usage, Description, Aliases]
            [Priority(0)]
            public async Task ClubAccept([Remainder]string userName)
            {
                if (_service.AcceptApplication(Context.User.Id, userName, out var discordUser))
                {
                    await ReplyConfirmLocalized("club_accepted", Format.Bold(discordUser.ToString())).ConfigureAwait(false);
                }
                else
                    await ReplyErrorLocalized("club_accept_error").ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            public async Task Clubleave()
            {
                if (_service.LeaveClub(Context.User))
                    await ReplyConfirmLocalized("club_left").ConfigureAwait(false);
                else
                    await ReplyErrorLocalized("club_not_in_club").ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [Priority(1)]
            public Task ClubKick([Remainder]IUser user)
                => ClubKick(user.ToString());

            [NadekoCommand, Usage, Description, Aliases]
            [Priority(0)]
            public Task ClubKick([Remainder]string userName)
            {
                if (_service.Kick(Context.User.Id, userName, out var club))
                    return ReplyConfirmLocalized("club_user_kick", Format.Bold(userName), Format.Bold(club.ToString()));
                else
                    return ReplyErrorLocalized("club_user_kick_fail");
            }

            [NadekoCommand, Usage, Description, Aliases]
            [Priority(1)]
            public Task ClubBan([Remainder]IUser user)
                => ClubBan(user.ToString());

            [NadekoCommand, Usage, Description, Aliases]
            [Priority(0)]
            public Task ClubBan([Remainder]string userName)
            {
                if (_service.Ban(Context.User.Id, userName, out var club))
                    return ReplyConfirmLocalized("club_user_banned", Format.Bold(userName), Format.Bold(club.ToString()));
                else
                    return ReplyErrorLocalized("club_user_ban_fail");
            }

            [NadekoCommand, Usage, Description, Aliases]
            [Priority(1)]
            public Task ClubUnBan([Remainder]IUser user)
                => ClubUnBan(user.ToString());

            [NadekoCommand, Usage, Description, Aliases]
            [Priority(0)]
            public Task ClubUnBan([Remainder]string userName)
            {
                if (_service.UnBan(Context.User.Id, userName, out var club))
                    return ReplyConfirmLocalized("club_user_unbanned", Format.Bold(userName), Format.Bold(club.ToString()));
                else
                    return ReplyErrorLocalized("club_user_unban_fail");
            }

            [NadekoCommand, Usage, Description, Aliases]
            public async Task ClubLevelReq(int level)
            {
                if (_service.ChangeClubLevelReq(Context.User.Id, level))
                {
                    await ReplyConfirmLocalized("club_level_req_changed", Format.Bold(level.ToString())).ConfigureAwait(false);
                }
                else
                {
                    await ReplyErrorLocalized("club_level_req_change_error").ConfigureAwait(false);
                }
            }

            [NadekoCommand, Usage, Description, Aliases]
            public async Task ClubDescription([Remainder] string desc = null)
            {
                if (_service.ChangeClubDescription(Context.User.Id, desc))
                {
                    await ReplyConfirmLocalized("club_desc_updated", Format.Bold(desc ?? "-")).ConfigureAwait(false);
                }
                else
                {
                    await ReplyErrorLocalized("club_desc_update_failed").ConfigureAwait(false);
                }
            }

            [NadekoCommand, Usage, Description, Aliases]
            public async Task ClubDisband()
            {
                if (_service.Disband(Context.User.Id, out ClubInfo club))
                {
                    await ReplyConfirmLocalized("club_disbanded", Format.Bold(club.ToString())).ConfigureAwait(false);
                }
                else
                {
                    await ReplyErrorLocalized("club_disband_error").ConfigureAwait(false);
                }
            }

            [NadekoCommand, Usage, Description, Aliases]
            public Task ClubLeaderboard(int page = 1)
            {
                if (--page < 0)
                    return Task.CompletedTask;

                var clubs = _service.GetClubLeaderboardPage(page);

                var embed = new EmbedBuilder()
                    .WithTitle(GetText("club_leaderboard", page + 1))
                    .WithOkColor();

                var i = page * 9;
                foreach (var club in clubs)
                {
                    embed.AddField($"#{++i} " + club.ToString(), club.Xp.ToString() + " xp", false);
                }

                return Context.Channel.EmbedAsync(embed);
            }
        }
    }
}