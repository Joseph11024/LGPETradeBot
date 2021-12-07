﻿using System;
using System.Collections;
using System.Text;
using Discord.Commands;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using PKHeX.Core;
using PKHeX.Core.AutoMod;
using PKHeX.Drawing;

using System.Linq;
using System.IO;

namespace SysBot.Pokemon.Discord
{
    

   public class TradeModule : ModuleBase<SocketCommandContext>
    {
        public static PokeTradeHub<PK8> Hub = SysCordInstance.Self.Hub;



        [Command("Trade")]
        [Alias("t")]
        [Summary("Trades You a pokemon from showdown text in Lets Go games")]
        public async Task Trade([Remainder]string ShowdownSet)
        {
            if (LetsGoTrades.discordID.Contains(Context.User.Id))
            {
                await ReplyAsync("you are already in queue");
                return;
            }
            var correctchannelcheck = Hub.Config.TradeBot.tradebotchannel.Split(',');
            if (!correctchannelcheck.Contains(Context.Channel.Id.ToString()))
            {
                await ReplyAsync("You can not use that command in this channel");
               
                return;
            }
            if (!EncounterEvent.Initialized)
                EncounterEvent.RefreshMGDB(Hub.Config.TradeBot.mgdbpath);
            APILegality.AllowBatchCommands = true;
            APILegality.AllowTrainerOverride = true;
            APILegality.ForceSpecifiedBall = true;
            APILegality.SetMatchingBalls = true;
            Legalizer.EnableEasterEggs = false;
            var set = new ShowdownSet(ShowdownSet);
     
           
           try
            {
                string[] pset = ShowdownSet.Split('\n');
                var pkm = (PB7)LetsGoTrades.sav.GetLegalFromSet(set, out var result);
                pkm.Stat_CP = pkm.CalcCP;
                if (pkm.Nickname.ToLower() == "egg" && Breeding.CanHatchAsEgg(pkm.Species))
                    pkm= EggTrade(pkm);
                if (pkm is not PB7 || !new LegalityAnalysis(pkm).Valid)
                {
                    var reason = result.ToString() == "Timeout" ? "That set took too long to generate." : "I wasn't able to create something from that.";
                    var imsg = $"Oops! {reason} Here's the legality report: ";
                    await Context.Channel.SendMessageAsync(imsg + new LegalityAnalysis(pkm).Report()).ConfigureAwait(false);
                    return;
                }
                if (ShowdownSet.Contains("AVs:"))
                {
                    
                    foreach(string c in pset)
                    {
                        if (c.Contains("AVs:"))
                        {
                           var avstats = c.Split(' ');
                            pkm.AV_HP = Convert.ToInt32(avstats[1]);
                            pkm.AV_ATK = Convert.ToInt32(avstats[4]);
                            pkm.AV_DEF = Convert.ToInt32(avstats[7]);
                            pkm.AV_SPA = Convert.ToInt32(avstats[10]);
                            pkm.AV_SPD = Convert.ToInt32(avstats[13]);
                            pkm.AV_SPE = Convert.ToInt32(avstats[16]);
                        }
                    }
                }
                if (!new LegalityAnalysis(pkm).Valid)
                    pkm = (PB7)pkm.Legalize();
                if (ShowdownSet.Contains("OT:"))
                {
                    
                    foreach (string b in pset)
                    {
                        if (b.Contains("OT:"))
                            pkm.OT_Name = b.Replace("OT: ", "");
                        
                    }
                }
                if (LegalityFormatting.GetLegalityReport(new LegalityAnalysis(pkm)).ToLower().Contains("ot name too long"))
                    pkm.OT_Name = "Pip";
                if (pkm.OT_Name == "PKHeX")
                    pkm.OT_Name = LetsGoTrades.sav.OT;
                if (ShowdownSet.Contains("TID:"))
                {

                   
                    foreach (string v in pset)
                    {
                        if (v.Contains("TID:"))
                        {
                            int trid7 = Convert.ToInt32(v.Replace("TID: ", ""));
                            pkm.TrainerID7 = trid7;

                        }
                       
                    }
                }
                if (ShowdownSet.Contains("SID:"))
                {
                    
                    foreach (string v in pset)
                    {
                        if (v.Contains("SID:"))
                        {
                            int trsid7 = Convert.ToInt32(v.Replace("SID: ", ""));
                            pkm.TrainerSID7 = trsid7;

                        }
                        
                    }
                }
                if (pkm.TrainerID7 == 993401)
                    pkm.TrainerID7 = LetsGoTrades.sav.TrainerID7;
                if (pkm.TrainerSID7 == 3559)
                    pkm.TrainerSID7 = LetsGoTrades.sav.TrainerSID7;
                if (ShowdownSet.ToLower().Contains("shiny: yes"))
                    pkm.SetShiny();

                if(!new LegalityAnalysis(pkm).Valid)
                {
                    var imsg = $"Oops! I wasn't able to create something from that. Here's the legality report: ";
                    await Context.Channel.SendMessageAsync(imsg + new LegalityAnalysis(pkm).Report()).ConfigureAwait(false);
                    return;
                }

                LetsGoTrades.discordname.Enqueue(Context.User);
                LetsGoTrades.discordID.Enqueue(Context.User.Id);
                LetsGoTrades.Channel.Enqueue(Context.Channel);
                LetsGoTrades.tradepkm.Enqueue(pkm);
                await Context.Message.DeleteAsync();
                await ReplyAsync($"{Context.User.Username} - Added to the LGPE Link Trade Queue. Current Position: {LetsGoTrades.discordID.Count}. Receiving: {(pkm.IsShiny ? "Shiny" : "")} {(Species)pkm.Species}{(pkm.Form == 0 ? "" : "-" + ShowdownParsing.GetStringFromForm(pkm.Form, GameInfo.Strings, pkm.Species, pkm.Format))}");
                
            } catch
            {
                var msg = $"Oops! An unexpected problem happened with this Showdown Set:\n```{string.Join("\n", set.GetSetLines())}```";
                await ReplyAsync(msg).ConfigureAwait(false);
            }
        }
  
        public static PB7 EggTrade(PB7 pk)
        {
           
            pk.IsNicknamed = true;
            pk.Nickname = pk.Language switch
            {
                1 => "タマゴ",
                3 => "Œuf",
                4 => "Uovo",
                5 => "Ei",
                7 => "Huevo",
                8 => "알",
                9 or 10 => "蛋",
                _ => "Egg",
            };

            pk.IsEgg = true;
            pk.Egg_Location = 60002;
            pk.MetDate = DateTime.Parse("2020/10/20");
            pk.EggMetDate = pk.MetDate;
            pk.HeldItem = 0;
            pk.CurrentLevel = 1;
            pk.EXP = 0;
           
            pk.Met_Level = 1;
            pk.Met_Location = 30002;
            pk.CurrentHandler = 0;
            pk.OT_Friendship = 1;
            pk.HT_Name = "";
            pk.HT_Friendship = 0;
           
            pk.HT_Gender = 0;
            pk.HT_Memory = 0;
            pk.HT_Feeling = 0;
            pk.HT_Intensity = 0;
            pk.StatNature = pk.Nature;
            pk.EVs = new int[] { 0, 0, 0, 0, 0, 0 };
            pk.Markings = new int[] { 0, 0, 0, 0, 0, 0, 0, 0 };
            pk.ClearRecordFlags();
            pk.ClearRelearnMoves();
            pk.Moves = new int[] { 0, 0, 0, 0 };
            var la = new LegalityAnalysis(pk);
            var enc = la.EncounterMatch;
            pk.CurrentFriendship = enc is EncounterStatic s ? s.EggCycles : pk.PersonalInfo.HatchCycles;
            pk.RelearnMoves = MoveBreed.GetExpectedMoves(pk.RelearnMoves, la.EncounterMatch);
            pk.Moves = pk.RelearnMoves;
            pk.Move1_PPUps = pk.Move2_PPUps = pk.Move3_PPUps = pk.Move4_PPUps = 0;
            pk.SetMaximumPPCurrent(pk.Moves);
            pk.SetSuggestedHyperTrainingData();
            pk.SetSuggestedRibbons(la.EncounterMatch);
            return pk;
        }

        [Command("Trade")]
        [Alias("t")]
       [Summary("Trades you a pokemon from a PB7 file in the Lets Go games")]
        public async Task filetrade()
        {
            
            if (LetsGoTrades.discordID.Contains(Context.User.Id))
            {
                await ReplyAsync("you are already in queue");
                return;
            }
            var correctchannelcheck = Hub.Config.TradeBot.tradebotchannel.Split(',');
            if (!correctchannelcheck.Contains(Context.Channel.Id.ToString()))
            {
                await ReplyAsync("You can not use that command in this channel");
                return;
            }
            if (!EncounterEvent.Initialized)
                EncounterEvent.RefreshMGDB(Hub.Config.TradeBot.mgdbpath);
            var attachment = Context.Message.Attachments.FirstOrDefault();
            if (attachment == default)
            {
                await ReplyAsync("No attachment provided!").ConfigureAwait(false);
                return;
            }

            var att = await NetUtil.DownloadPKMAsync(attachment).ConfigureAwait(false);
            
            if (att == null)
            {
                await ReplyAsync("something went wrong with grabbing your attachment");
                return;
            }
            var pkm = GetRequest(att);
           

            if (pkm is not PB7 || !new LegalityAnalysis(pkm).Valid)
            {
                
                var imsg = $"Oops! This file is illegal Here's the legality report: ";
                await Context.Channel.SendMessageAsync(imsg + new LegalityAnalysis(pkm).Report()).ConfigureAwait(false);
                return;
            }
            
            LetsGoTrades.discordname.Enqueue(Context.User);
            LetsGoTrades.discordID.Enqueue(Context.User.Id);
            LetsGoTrades.Channel.Enqueue(Context.Channel);
            LetsGoTrades.tradepkm.Enqueue(pkm);
            await Context.Message.DeleteAsync();
            await ReplyAsync($"{Context.User.Username} - Added to the LGPE Link Trade Queue. Current Position: {LetsGoTrades.discordID.Count}. Receiving: {(pkm.IsShiny ? "Shiny" : "")} {(Species)pkm.Species}{(pkm.Form == 0 ? "" : "-" + ShowdownParsing.GetStringFromForm(pkm.Form, GameInfo.Strings, pkm.Species, pkm.Format))}");
            
        }
        private static PB7? GetRequest(Download<PKM> dl)
        {
            if (!dl.Success)
                return null;
            return dl.Data switch
            {
                null => null,
                PB7 pkm => pkm,
                _ => PKMConverter.ConvertToType(dl.Data, typeof(PB7), out _) as PB7
            };
        }

        [Command("queue")]
        [Alias("q")]
        public async Task queue()
        {
            Object[] arr = LetsGoTrades.discordname.ToArray();
            var sb = new System.Text.StringBuilder();
            var embed = new EmbedBuilder();
            if (arr.Length == 0)
            {
                await ReplyAsync("queue is empty");
            }
            int r = 0;
            foreach (object i in arr)
            {

                sb.AppendLine((r + 1).ToString() + ". " + arr[r].ToString());
                r++;
            }
            embed.AddField(x =>
            {

                x.Name = "Trade Queue:";
                x.Value = sb.ToString();
                x.IsInline = false;


            });
            await ReplyAsync(embed: embed.Build());
        }

        [Command("convert")]
        [Alias("c")]
        [Summary("Makes a PB7 file for you from showdown text, great way to check legality.")]
        public async Task pbjmaker([Remainder] string ShowdownSet)
        {
            if (!EncounterEvent.Initialized)
                EncounterEvent.RefreshMGDB(Hub.Config.TradeBot.mgdbpath);
            APILegality.AllowBatchCommands = true;
            APILegality.AllowTrainerOverride = true;
            APILegality.ForceSpecifiedBall = true;
            APILegality.SetMatchingBalls = true;

            var set = new ShowdownSet(ShowdownSet);

            try
            {
                string[] pset = ShowdownSet.Split('\n');
                var pkm = (PB7)LetsGoTrades.sav.GetLegalFromSet(set, out var result);
                pkm.Stat_CP = pkm.CalcCP;
                if (pkm.Nickname.ToLower() == "egg" && Breeding.CanHatchAsEgg(pkm.Species))
                    pkm = EggTrade(pkm);
                string temppokewait = Path.GetTempFileName().Replace(".tmp", $"{GameInfo.Strings.Species[pkm.Species]}.{pkm.Extension}").Replace("tmp", "");


                if (ShowdownSet.Contains("AVs:"))
                {

                    foreach (string c in pset)
                    {
                        if (c.Contains("AVs:"))
                        {
                            var avstats = c.Split(' ');
                            pkm.AV_HP = Convert.ToInt32(avstats[1]);
                            pkm.AV_ATK = Convert.ToInt32(avstats[4]);
                            pkm.AV_DEF = Convert.ToInt32(avstats[7]);
                            pkm.AV_SPA = Convert.ToInt32(avstats[10]);
                            pkm.AV_SPD = Convert.ToInt32(avstats[13]);
                            pkm.AV_SPE = Convert.ToInt32(avstats[16]);
                        }
                    }
                }
                else
                    pkm.AwakeningSetAllTo(200);


                if (ShowdownSet.Contains("OT:"))
                {
                    int q = 0;
                    foreach (string b in pset)
                    {
                        if (pset[q].Contains("OT:"))
                            pkm.OT_Name = pset[q].Replace("OT: ", "");
                        q++;
                    }
                }
                if (LegalityFormatting.GetLegalityReport(new LegalityAnalysis(pkm)).ToLower().Contains("ot name too long"))
                    pkm.OT_Name = "Pip";
                if (ShowdownSet.Contains("TID:"))
                {

                    int h = 0;
                    foreach (string v in pset)
                    {
                        if (pset[h].Contains("TID:"))
                        {
                            int trid7 = Convert.ToInt32(pset[h].Replace("TID: ", ""));
                            pkm.TrainerID7 = trid7;

                        }
                        h++;
                    }
                }
                if (ShowdownSet.Contains("SID:"))
                {
                    int h = 0;
                    foreach (string v in pset)
                    {
                        if (pset[h].Contains("SID:"))
                        {
                            int trsid7 = Convert.ToInt32(pset[h].Replace("SID: ", ""));
                            pkm.TrainerSID7 = trsid7;

                        }
                        h++;
                    }
                }
                if (ShowdownSet.ToLower().Contains("shiny: yes"))
                {
                    pkm.SetIsShiny(true);
                }
                if (!new LegalityAnalysis(pkm).Valid)
                {
                    await ReplyAsync("I could not legalize that set");
                    File.Delete(temppokewait);
                    return;
                }


                byte[] yre = pkm.DecryptedBoxData;
                File.WriteAllBytes(temppokewait, yre);
                await Context.Channel.SendFileAsync(temppokewait, "Here is your legalized pk file");
                File.Delete(temppokewait);
                return;

            }
            catch
            {
                await Context.Channel.SendMessageAsync("I wasn't able to make a file from that set");
            }
        }
    }
}
