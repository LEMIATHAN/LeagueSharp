﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Runtime.CompilerServices;
using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;
using Color = System.Drawing.Color;

namespace MysticalBard
{
    internal class Program
    {
        public static Menu Menu;
        private static Obj_AI_Hero Player;


        public static Orbwalking.Orbwalker Orbwalker;

        public static Spell Q;
        public static Spell R;


        public static Spell stunQ { get; private set; }


        public static void Main(string[] args)
        {
            Game.OnStart += Game_Start;
            if (Game.Mode == GameMode.Running)
            {
                Game_Start(new EventArgs());
            }
        }
        
        public static void Game_Start(EventArgs args)
        {
            Player = ObjectManager.Player;

            if (Player.ChampionName != "Bard")
                return;

            Menu = new Menu("Bard", "Bard", true);
            var TargetSelectorMenu = new Menu("Target Selector", "Target Selector");
            TargetSelector.AddToMenu(TargetSelectorMenu);
            Menu.AddSubMenu(TargetSelectorMenu);


            Menu.AddSubMenu(new Menu("Orbwalking", "Orbwalking"));
            Orbwalker = new Orbwalking.Orbwalker(Menu.SubMenu("Orbwalking"));

            Menu.AddSubMenu(new Menu("Combo", "Combo"));
            Menu.SubMenu("Combo").AddItem(new MenuItem("UseQ", "Use Q").SetValue(true));
            Menu.SubMenu("Combo").AddItem(new MenuItem("UseR", "Use R").SetValue(true));
            Menu.SubMenu("Combo").AddItem(new MenuItem("Stun", "Q stun restriction")).SetValue(true);
            Menu.SubMenu("Combo").AddItem(new MenuItem("MinEnemies", "Minimum enemies for ult")).SetValue(new Slider(3, 5, 1));
            Menu.SubMenu("Combo").AddItem(new MenuItem("cActive", "Combo").SetValue(new KeyBind(32, KeyBindType.Press)));


            Menu.AddSubMenu(new Menu("Harass", "Harass"));
            Menu.SubMenu("Harass").AddItem(new MenuItem("HarassQ", "Use Q").SetValue(false));
            Menu.SubMenu("Harass").AddItem(new MenuItem("hActive", "Harass").SetValue(new KeyBind("20".ToCharArray()[0], KeyBindType.Press)));


            Menu.AddSubMenu(new Menu("WClear", "WClear"));
            Menu.SubMenu("WClear").AddItem(new MenuItem("FarmQ", "Use Q").SetValue(false));
            Menu.SubMenu("WClear").AddItem(new MenuItem("wActive", "Wave Clear").SetValue(new KeyBind("V".ToCharArray()[0], KeyBindType.Press)));

            // Taken from DesomondBard. Credit goes to Desomond, I had no idea what I was doing here :P vvvvvvvv
            var mana = Menu.AddSubMenu(new Menu("Misc", "Misc"));
            mana.AddItem(new MenuItem("comboMana", "Combo Mana").SetValue(new Slider(1, 100, 0)));
            mana.AddItem(new MenuItem("harassMana", "Harass Mana").SetValue(new Slider(30, 100, 0)));
            mana.AddItem(new MenuItem("forceQ", "Force Q(slow not guarenteed").SetValue(new KeyBind("A".ToCharArray()[0], KeyBindType.Press)));
            mana.AddItem(new MenuItem("forceR", "Force R").SetValue(new KeyBind("T".ToCharArray()[0], KeyBindType.Press)));
            mana.AddItem(new MenuItem("interruptR", "interrupt dangerous spells with Ult").SetValue(true));


            Menu.AddSubMenu(new Menu("Draw", "Draw"));
            Menu.SubMenu("Draw").AddItem(new MenuItem("DrawQ", "Draw Q").SetValue(new Circle(true, System.Drawing.Color.Green)));
            Menu.SubMenu("Draw").AddItem(new MenuItem("DrawQ", "Draw Q").SetValue(new Circle(true, System.Drawing.Color.Green)));


            Menu.AddToMainMenu();


            Q = new Spell(SpellSlot.Q, 950f);
            R = new Spell(SpellSlot.R, 2500f);
            stunQ = new Spell(SpellSlot.Q, Q.Range);

            Q.SetSkillshot(0.25f, 60, 1600, false, SkillshotType.SkillshotLine);
            R.SetSkillshot(0.5f, 325, 2100, false, SkillshotType.SkillshotCircle);
            stunQ.SetSkillshot(Q.Delay, Q.Width, Q.Speed, true, SkillshotType.SkillshotLine);


            Game.PrintChat("MysticalBard Loaded.");
            Game.OnUpdate += Game_OnUpdate;
            Drawing.OnDraw += OnDraw;
            Interrupter2.OnInterruptableTarget += BardOnInterruptableSpell;

        }


        public static void Game_OnUpdate(EventArgs args)
        {

            Obj_AI_Hero t = null;
            var wActive = Menu.Item("wActive").GetValue<KeyBind>().Active;
            var hActive = Menu.Item("hActive").GetValue<KeyBind>().Active;
            var cActive = Menu.Item("cActive").GetValue<KeyBind>().Active;
            var harassMana = Menu.Item("harassMana").GetValue<Slider>().Value;
            var comboMana = Menu.Item("comboMana").GetValue<Slider>().Value;

            var forceQ = Menu.Item("forceQ").GetValue<KeyBind>().Active;

            var forceR = Menu.Item("forceR").GetValue<KeyBind>().Active;

            if (wActive)
            {
                Farm();
            }
            if (hActive && harassMana < ((ObjectManager.Player.Mana / ObjectManager.Player.MaxMana) * 100))
            {
                Harass(t);
            }
            if (cActive && comboMana < ((ObjectManager.Player.Mana / ObjectManager.Player.MaxMana) * 100))
            {
                Combo(t);
            }

            if (Q.IsReady() && forceQ)
            {
                t = TargetSelector.GetTarget(Q.Range, TargetSelector.DamageType.Magical);
                if (t.IsValidTarget())
                {
                    Q.Cast(t);
                }
            }
    
            if (R.IsReady() && forceR)
            {
                t = TargetSelector.GetTarget(R.Range, TargetSelector.DamageType.Magical);
                if (t.IsValidTarget())
                {
                    R.Cast(t);
                }
            }
        }

        public static void Farm()
        {
            List<Vector2> pos = new List<Vector2>();
            bool qFarm = Menu.Item("FarmQ").GetValue<bool>();

            var AllMinions = MinionManager.GetMinions(Player.ServerPosition, Q.Range, MinionTypes.All, MinionTeam.Enemy, MinionOrderTypes.Health);
            foreach (var minion in AllMinions)
            {
                if (qFarm && Q.IsReady())
                {
                    Q.Cast(minion);
                }
            }
        }


        public static void Harass(Obj_AI_Hero t)
        {
            var HarassQ = Menu.Item("HarassQ").GetValue<bool>();
            var alwaysStun = Menu.Item("alwaysStun").GetValue<bool>();
            t = TargetSelector.GetTarget(Q.Range, TargetSelector.DamageType.Magical);

            if (HarassQ && Q.IsReady())
            {
                if (t.IsValidTarget())
                {
                    if (alwaysStun)
                    {
                        castStunQ(t);
                    }
                    else
                    {
                        Q.Cast(t);
                    }
                }
            }
        }

        public static void Combo(Obj_AI_Hero t)
        {

            var useQ = Menu.Item("UseQ").GetValue<bool>();
            var useR = Menu.Item("UseR").GetValue<bool>();
            var alwaysStun = Menu.Item("alwaysStun").GetValue<bool>();
            var numOfEnemies = Menu.Item("MinEnemies").GetValue<Slider>().Value;
            t = TargetSelector.GetTarget(Q.Range, TargetSelector.DamageType.Magical);

            if (useQ && Q.IsReady())
            {
                if (t.IsValidTarget())
                {
                    if (alwaysStun)
                    {
                        castStunQ(t);
                    }
                    else
                    {
                        Q.Cast(t);
                    }
                }
            }
            if (useR && R.IsReady())
            {
                var t2 = TargetSelector.GetTarget(2500, TargetSelector.DamageType.Magical);
                if (GetEnemies(t2) >= numOfEnemies)
                {
                    R.Cast(t2, false, true);
                }

            }
        }


        private static int GetEnemies(Obj_AI_Hero target)
        {
            int Enemies = 0;
            foreach (Obj_AI_Hero enemies in ObjectManager.Get<Obj_AI_Hero>())
            {
                var pred = R.GetPrediction(enemies, true);
                if (pred.Hitchance >= HitChance.High && !enemies.IsMe && enemies.IsEnemy && Vector3.Distance(Player.Position, pred.UnitPosition) <= R.Range)
                {
                    Enemies = Enemies + 1;
                }
            }
            return Enemies;
        }


        private static void BardOnInterruptableSpell(Obj_AI_Hero unit, Interrupter2.InterruptableTargetEventArgs args)
        {
            if (Menu.Item("interruptR").GetValue<bool>())
            {
                R.Cast(unit, false, true);
            }
        }


        private static void OnDraw(EventArgs args)
        {

            if (Menu.Item("DrawQ").GetValue<bool>())
            {
                Render.Circle.DrawCircle(Player.Position, Q.Range, Menu.SubMenu("Drawing").Item("drawQRange").GetValue<Circle>().Color);
            }
            if (Menu.Item("DrawR").GetValue<bool>() && Player.Level >= 6)
            {
                Render.Circle.DrawCircle(ObjectManager.Player.Position, R.Range, Menu.SubMenu("Drawing").Item("drawRRange").GetValue<Circle>().Color);
            }
        }


        private static void castStunQ(Obj_AI_Hero target)
        {
            var prediction = stunQ.GetPrediction(target);

            var direction = (Player.ServerPosition - prediction.UnitPosition).Normalized();
            var endOfQ = (Q.Range)*direction;

            var checkPoint = prediction.UnitPosition.Extend(Player.ServerPosition, -Q.Range/2);

            if ((prediction.UnitPosition.GetFirstWallPoint(checkPoint).HasValue) || (prediction.CollisionObjects.Count == 1))
            {
                Q.Cast(prediction.UnitPosition);
            }
        }
    }
}
