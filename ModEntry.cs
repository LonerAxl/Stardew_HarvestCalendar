using Microsoft.CodeAnalysis;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Menus;
using StardewValley.TerrainFeatures;
using System.Linq;
using HarvestCalendar.Framework;

namespace HarvestCalendar
{
    /// <summary>The mod entry point.</summary>
    internal sealed class ModEntry : Mod
    {

        Dictionary<int, CalendarDayItem> CalendarDayDict = new();
        internal Configuration Config = null!;

        /*********
        ** Public methods
        *********/
        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            I18n.Init(helper.Translation);
            this.Config = helper.ReadConfig<Configuration>();

            helper.Events.GameLoop.GameLaunched += OnGameLaunched;

            helper.Events.Display.MenuChanged += this.OnCalendarOpen;
            helper.Events.Display.MenuChanged += this.OnCalendarClosed;
            helper.Events.Display.RenderedActiveMenu += this.OnRenderedActiveCalendar;
            
        }


        /*********
        ** Private methods
        *********/
        /// <summary>
        /// Set up Generic Mod Config Menu
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            var configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu == null)
                return;
            configMenu.Register(
                mod: this.ModManifest,
                reset: () => this.Config = new Configuration(),
                save: () => this.Helper.WriteConfig(this.Config)
            );

            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => Helper.Translation.Get("ToggleMod"),
                tooltip: () => Helper.Translation.Get("ToggleMod.Desccription"),
                getValue: () => this.Config.ToggleMod,
                setValue: value => this.Config.ToggleMod = value
            );

            configMenu.AddNumberOption(
                mod: this.ModManifest,
                name: () => Helper.Translation.Get("IconSize"),
                tooltip: () => Helper.Translation.Get("IconSize.Desccription"),
                getValue: () => this.Config.IconSize,
                setValue: value => this.Config.IconSize = (int)value,
                min: 1, max: 4,
                interval: 1,
                formatValue: value => {
                    string[] _ = { Helper.Translation.Get("IconSize.Small"),
                                Helper.Translation.Get("IconSize.Medium"),
                                Helper.Translation.Get("IconSize.Large"),
                                Helper.Translation.Get("IconSize.XLarge") };
                    return _[(int)value - 1];
                }
            );
            configMenu.AddNumberOption(
                mod: this.ModManifest,
                name: () => Helper.Translation.Get("IconPositionX"),
                tooltip: () => Helper.Translation.Get("IconPositionX.Description"),
                getValue: () => this.Config.IconX,
                setValue: value => this.Config.IconX = value,
                min: 0f, max: 1f,
                interval: 0.1f
            );
            configMenu.AddNumberOption(
                mod: this.ModManifest,
                name: () => Helper.Translation.Get("IconPositionY"),
                tooltip: () => Helper.Translation.Get("IconPOsitionY.Description"),
                getValue: () => this.Config.IconY,
                setValue: value => this.Config.IconY = value,
                min: 0f, max: 1f,
                interval: 0.1f
            );


        }



        /// <summary>when the player open the Calendar, calculate all crops on the field and predict the harvest day then show it on the calendar.</summary>
        private void OnCalendarOpen(object? sender, MenuChangedEventArgs e) 
        {
            if (!this.Config.ToggleMod)
                return;
            // Check if is on Calendar menu
            if (this.isCalendarPage())
            {
                if (this.CalendarDayDict.Count != 0)
                    return;

                // TODO: Currently not support multiplayer as farmhands can't get all locations? Not sure

                int today = Game1.dayOfMonth;
                Dictionary<(int, string, string), int> allCropsHarvestDay = new();

                foreach (GameLocation location in Game1.locations)
                {
                    if (!(location.IsFarm || location.IsGreenhouse || location.InIslandContext()))
                        continue;
                    allCropsHarvestDay = allCropsHarvestDay.Concat(GetAllCropsbyLocation(location)).ToDictionary(kvp => kvp.Key, kvp=> kvp.Value);
                }

                // sum up number of crops by day and cropId, to get the cropId with the largest amount, so that the icon will be showed on the calendar
                var iconQuery = allCropsHarvestDay.GroupBy(x => new { day = x.Key.Item1, cropId = x.Key.Item3 })
                                                .Select(g => new
                                                {
                                                    g.Key.day,
                                                    g.Key.cropId,
                                                    count = g.Sum(t => t.Value)
                                                }).OrderBy(g=>g.day).ThenByDescending(g=>g.count).GroupBy(g=>g.day);

                // sum up number of crops by day and location, so that the detail can be showed as text (TODO)
                // to show numbers by just location or by location+cropId, not decided
                // if show text by hovering on icon, will pick location only as the text will be shorter
                // if show text by clicking icon and open up a new menu, then a much more detailed text might be a better choice
                var countQuery = allCropsHarvestDay.GroupBy(x => new { day = x.Key.Item1, location = x.Key.Item2 })
                                                .Select(g => new {
                                                    g.Key.day,
                                                    g.Key.location,
                                                    count = g.Sum(t => t.Value)
                                                }).GroupBy(x => x.day);

                foreach (var i in iconQuery)
                {
                    foreach (var j in i)
                    {
                        CalendarDayDict.Add(j.day, new CalendarDayItem(j.day, j.cropId));
                        break;
                    }
                }

                foreach (var i in countQuery)
                {
                    foreach (var j in i)
                    {
                        CalendarDayDict[j.day].AddCrops(j.location, j.count);
                    }
                }
                
            } 
        }


        /// <summary>
        /// draw the icon on calendar
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnRenderedActiveCalendar(object? sender, RenderedActiveMenuEventArgs e) 
        {
            if (!this.Config.ToggleMod)
                return;
            if (!isCalendarPage())
                return;

            Billboard billboard = Game1.activeClickableMenu as Billboard;
            List<ClickableTextureComponent> days = billboard.calendarDays;
            

            int today = Game1.dayOfMonth;
            for (int i = today; i <= 28; i++) 
            {
                if (!CalendarDayDict.ContainsKey(i))
                    continue;
                CalendarDayItem item = CalendarDayDict[i];
                var produce = ItemRegistry.GetDataOrErrorItem(item.iconId);
                var iconTexture = produce.GetTexture();

                int offsetX = days[i-1].bounds.Width / 10 * this.Config.IconSize;
                int offsetY = days[i - 1].bounds.Height / 10 * this.Config.IconSize;

                // position hard coded to be top right corner
                // three posible position in the future: topright, middleleft, bottomleft
                // topright might be the best
                //Vector2 position = new Vector2(days[i - 1].bounds.Right - offsetX, days[i - 1].bounds.Bottom - offsetY);
                Vector2 position = new Vector2((days[i - 1].bounds.Width - offsetX)*this.Config.IconX + days[i-1].bounds.Left,
                                                (days[i - 1].bounds.Height - offsetY) * this.Config.IconY + days[i-1].bounds.Top);

                e.SpriteBatch.Draw(iconTexture, new Rectangle((int)position.X, (int)position.Y, offsetX, offsetY), produce.GetSourceRect(), Color.White);


                // TODO: show message when hover on the icon or click on the day
                // currently just show some on console
                if (i > today + 3)
                    continue;
                string newHoverText = string.Empty;
                newHoverText += $"On Day {i}:";
                item.locationCrops.ForEach(lc =>
                {
                    newHoverText += Environment.NewLine;
                    newHoverText += $"{lc.Item1} has {lc.Item2} crops to be harvested";
                });
                this.Monitor.LogOnce($"{newHoverText}", LogLevel.Debug);
            }

            // Redraw the cursor
            billboard.drawMouse(e.SpriteBatch);

            string text = Helper.Reflection.GetField<string>(billboard, "hoverText").GetValue();
            // Redraw the hover text
            if (text.Length > 0)
            {
                IClickableMenu.drawHoverText(e.SpriteBatch, text, Game1.dialogueFont);
            }


        }


        /// <summary>
        ///  when closing the menu, reset the dictionary
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnCalendarClosed(object? sender, MenuChangedEventArgs e) 
        {
            if (!this.Config.ToggleMod)
                return;
            if (Game1.activeClickableMenu == null && e.OldMenu is Billboard)
                this.CalendarDayDict = new();
            
        }


 

        /// <summary>
        /// a simple class that store the calculated harvest data for a day
        /// </summary>
        internal class CalendarDayItem 
        {
            public int dayOfMonth;
            public string iconId;
            public List<(string, int)> locationCrops;

            public CalendarDayItem(int dayOfMonth, string id)
            {
                this.dayOfMonth = dayOfMonth;
                this.iconId = id;
                this.locationCrops = new();
            }

            public void AddCrops(string location, int count) 
            {
                this.locationCrops.Add((location, count));
            }
        }



        /// <summary>Return if Calendar is open or not.</summary>
        private bool isCalendarPage()
        {
            return
                Game1.activeClickableMenu is Billboard;
        }



        /// copied and modified from gottyduke's stardew-informant mod
        /// https://github.com/gottyduke/stardew-informant/blob/main/Informant/Implementation/TooltipGenerator/CropTooltipGenerator.cs
        /// <summary>Return days left until harvest</summary>
        /// <param name="crop">The crop.</param>
        /// <returns>a tuple with two values, the first one is the days left until harvest, the second one is the days of regrow, -1 if not regrowable</returns>
        internal (int, int) CalculateDaysLeft(Crop crop)
        {
            var currentPhase = crop.currentPhase.Value;
            var dayOfCurrentPhase = crop.dayOfCurrentPhase.Value;
            var regrowAfterHarvest = crop.RegrowsAfterHarvest();
            var cropPhaseDays = crop.phaseDays.ToArray();
            // Amaranth:  current = 4 | day = 0 | days = 1, 2, 2, 2, 99999 | result => 0
            // Fairy Rose:  current = 4 | day = 1 | days = 1, 4, 4, 3, 99999 | result => 0
            // Cranberry:  current = 5 | day = 4 | days = 1, 2, 1, 1, 2, 99999 | result => ???
            // Ancient Fruit: current = 5 | day = 4 | days = 1 5 5 6 4 99999 | result => 4
            // Blueberry (harvested): current = 5 | day = 4 | days = 1 3 3 4 2 99999 | regrowAfterHarvest = 4 | result => 4
            // Blueberry (harvested): current = 5 | day = 0 | days = 1 3 3 4 2 99999 | regrowAfterHarvest = 4 | result => 0
            var result = 0;
            if (crop.Dirt.readyForHarvest()) 
            {
                return (result, crop.RegrowsAfterHarvest() ? crop.GetData().RegrowDays : -1);
            }
            for (var phase = currentPhase; phase < cropPhaseDays.Length; phase++)
            {
                if (cropPhaseDays[phase] < 99999)
                {
                    result += cropPhaseDays[phase];
                    if (phase == currentPhase)
                    {
                        result -= dayOfCurrentPhase;
                    }
                }
                else if (currentPhase == cropPhaseDays.Length - 1 && regrowAfterHarvest)
                {
                    // calculate the repeating harvests, it seems the dayOfCurrentPhase counts backwards now
                    result = dayOfCurrentPhase;
                }
            }

            return (result, crop.GetData().RegrowDays);
        }



        /// <summary>
        /// return all crops harvest data for a location
        /// </summary>
        /// <param name="location"></param>
        /// <returns>return a dictionary, key: (harvestDayOfMonth, locationName, cropId), value: numberOfCrops</returns>
        internal Dictionary<(int, string, string), int> GetAllCropsbyLocation(GameLocation location)
        {

            Dictionary<(int, string, string), int> result = new();
            int today = Game1.dayOfMonth;
            
            foreach (TerrainFeature value in location.terrainFeatures.Values)
            {
                HoeDirt hoeDirt = value as HoeDirt;
                if (hoeDirt != null)
                {
                    Crop crop = hoeDirt.crop;
                    if (crop == null || crop.dead.Value || crop.whichForageCrop.Value == Crop.forageCrop_gingerID)
                        continue;
                    (int, int) days = CalculateDaysLeft(crop);

                    var cropId = crop.indexOfHarvest.Value;
                    var locationName = location.NameOrUniqueName;

                    for (int i = today + days.Item1; i <= 28 && i >= today + days.Item1; i += days.Item2)
                    {
                        if (result.ContainsKey((i, locationName, cropId)))
                        {
                            result[(i, locationName, cropId)] += 1;
                        }
                        else 
                        {
                            result.Add((i, locationName, cropId), 1);
                        }
                    }
                }
            }

            //var query = result.OrderBy(x => x.Key.Item1).ThenBy(x=>x.Key.Item2);
            //foreach (var item in query)
            //{
            //    if (item.Key.Item1 > Game1.dayOfMonth + 7)
            //        break;
            //    this.Monitor.Log($"On {item.Key.Item1}, {item.Key.Item2} has {item.Value} {ItemRegistry.GetDataOrErrorItem(item.Key.Item3).DisplayName}!", LogLevel.Debug);
            //}

            return result;
        }




    }
}