using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace Long_game
{
    internal enum PlayMode
    {
        Title,
        Playing,
        Paused,
        Dead,
        Won
    }

    internal sealed class Player
    {
        public float x;
        public float y;
        public float vx;
        public float vy;
        public int face = 1;
        public int hp = 100;
        public int ammo = 12;
        public int reserve = 72;
        public float invulnerable;
        public float fireTimer;
        public float reloadTimer;
        public float dashTimer;
        public float dashCooldown;
        public float meleeTimer;
        public float meleeCooldown;
        public float animation;
        public bool onGround;
        public bool reloading;

        public const float BodyWidth = 58;
        public const float BodyHeight = 100;
    }

    internal sealed class Enemy
    {
        public float x;
        public float y;
        public float hp;
        public float maxhp;
        public float speed;
        public float attackCooldown;
        public float attackTimer;
        public float hurtTimer;
        public float animation;
        public int kind;
        public int face = -1;
        public bool alive = true;
        public bool isBoss;

        public Enemy(float px, float py, int enemyKind, float health, float moveSpeed, bool boss)
        {
            x = px;
            y = py;
            kind = enemyKind;
            hp = health;
            maxhp = health;
            speed = moveSpeed;
            isBoss = boss;
            attackCooldown = boss ? 2.0f : 0.9f;
        }
    }

    internal sealed class Shot
    {
        public float x;
        public float y;
        public float vx;
        public float life = 1.6f;
        public bool hostile;
        public int damage;
    }

    internal sealed class Pickup
    {
        public float x;
        public float y;
        public int kind;
        public bool taken;
        public float bob;

        public Pickup(float px, float py, int pickupKind)
        {
            x = px;
            y = py;
            kind = pickupKind;
        }
    }

    internal sealed class Particle
    {
        public float x;
        public float y;
        public float vx;
        public float vy;
        public float life;
        public float size;
        public Color color;
    }

    internal sealed class Platform
    {
        public float x;
        public float top;
        public float width;
        public float height;

        public Platform(float px, float ptop, float pw, float ph)
        {
            x = px;
            top = ptop;
            width = pw;
            height = ph;
        }
    }

    public sealed class GameForm : Form
    {
        private const float WorldWidth = 6300;
        private const float PlayerDrawWidth = 82;
        private const float PlayerDrawHeight = 113;
        private const float WalkSpeed = 365;

        private readonly Timer timer = new Timer();
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly Random random = new Random(23);
        private readonly Dictionary<string, Bitmap> images = new Dictionary<string, Bitmap>();
        private readonly List<Enemy> enemies = new List<Enemy>();
        private readonly List<Shot> shots = new List<Shot>();
        private readonly List<Pickup> pickups = new List<Pickup>();
        private readonly List<Particle> particles = new List<Particle>();
        private readonly List<Platform> platforms = new List<Platform>();
        private readonly List<int> skylineHeights = new List<int>();

        private Player hero;
        private Enemy boss;
        private PlayMode mode = PlayMode.Title;
        private Bitmap toxicWalkA;
        private Bitmap toxicAttackA;
        private Bitmap toxicWalkB;
        private Bitmap toxicWalkC;
        private float cameraX;
        private float groundY;
        private float bossWarning;
        private float bossAttackTimer = 1.5f;
        private float bossWindup;
        private float bossFlash;
        private float shakeTimer;
        private float shakePower;
        private float gameTime;
        private float introFade = 1;
        private int score;
        private int kills;
        private int bestScore;
        private int levelExitX = 6030;
        private bool leftDown;
        private bool rightDown;
        private bool fireDown;
        private bool jumpDown;
        private bool meleeDown;
        private bool controlFireDown;

        public GameForm()
        {
            Text = "LONG GAME: LAST STAND";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1360, 820);
            MinimumSize = new Size(960, 620);
            WindowState = FormWindowState.Maximized;
            KeyPreview = true;
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

            LoadAssets();
            MakeSkyline();
            ResetGame();

            timer.Interval = 16;
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        private void LoadAssets()
        {
            string[] names = new string[]
            {
                "Player/stand.png", "Player/walk1.png", "Player/walk2.png", "Player/jump.png",
                "Player/hurt.png", "Player/shoot.png", "Player/melee.png",
                "Zombies/idle.png", "Zombies/walk1.png", "Zombies/walk2.png", "Zombies/attack1.png",
                "Zombies/attack2.png", "Zombies/hurt.png",
                "World/brickGrey.png", "World/boxCrate.png", "World/boxCoin.png", "World/doorOpen_top.png",
                "World/doorOpen_mid.png", "World/spikes.png"
            };

            for (int i = 0; i < names.Length; i++)
            {
                string fullPath = Path.Combine(Application.StartupPath, "Assets", names[i].Replace('/', Path.DirectorySeparatorChar));
                images[names[i]] = new Bitmap(fullPath);
            }

            toxicWalkA = new Bitmap(Path.Combine(Application.StartupPath, "Assets", "Enemies", "zombie_typeA_walk.png"));
            toxicAttackA = new Bitmap(Path.Combine(Application.StartupPath, "Assets", "Enemies", "zombie_typeA_attack.png"));
            toxicWalkB = new Bitmap(Path.Combine(Application.StartupPath, "Assets", "Enemies", "zombie_typeB_walk.png"));
            toxicWalkC = new Bitmap(Path.Combine(Application.StartupPath, "Assets", "Enemies", "zombie_typeC_walk.png"));
        }

        private Bitmap Img(string name)
        {
            return images[name];
        }

        private void MakeSkyline()
        {
            for (int i = 0; i < 60; i++)
                skylineHeights.Add(random.Next(75, 300));
        }

        private float GroundLine
        {
            get { return Math.Max(470, ClientSize.Height - 118); }
        }

        private void ResetGame()
        {
            groundY = GroundLine;
            hero = new Player();
            hero.x = 150;
            hero.y = groundY - Player.BodyHeight;
            hero.onGround = true;
            cameraX = 0;
            gameTime = 0;
            score = 0;
            kills = 0;
            bossWarning = 0;
            bossAttackTimer = 1.5f;
            bossWindup = 0;
            introFade = 1;
            shakeTimer = 0;
            leftDown = rightDown = fireDown = jumpDown = meleeDown = controlFireDown = false;
            enemies.Clear();
            shots.Clear();
            pickups.Clear();
            particles.Clear();
            platforms.Clear();

            AddPlatform(550, 165, 250);
            AddPlatform(1080, 215, 210);
            AddPlatform(1620, 145, 250);
            AddPlatform(2240, 205, 280);
            AddPlatform(2860, 155, 230);
            AddPlatform(3440, 220, 300);
            AddPlatform(4100, 170, 240);
            AddPlatform(4680, 215, 300);

            AddEnemy(760, 0, 0, 550, 132);
            AddEnemy(1260, 0, 1, 700, 178);
            AddEnemy(1830, 0, 0, 580, 156);
            AddEnemy(2360, 0, 2, 820, 260);
            AddEnemy(2950, 0, 1, 760, 248);
            AddEnemy(3520, 0, 0, 620, 205);
            AddEnemy(4140, 0, 3, 880, 270);
            AddEnemy(4650, 0, 1, 840, 255);
            AddEnemy(4920, 0, 0, 650, 205);
            boss = new Enemy(5440, groundY - 144, 3, 2520, 402, true);
            boss.face = -1;

            pickups.Add(new Pickup(900, groundY - 42, 1));
            pickups.Add(new Pickup(1510, groundY - 42, 0));
            pickups.Add(new Pickup(2500, groundY - 42, 1));
            pickups.Add(new Pickup(3210, groundY - 42, 0));
            pickups.Add(new Pickup(3900, groundY - 42, 1));
            pickups.Add(new Pickup(4550, groundY - 42, 0));
            mode = PlayMode.Title;
        }

        private void AddPlatform(float x, float heightAboveGround, float width)
        {
            platforms.Add(new Platform(x, groundY - heightAboveGround, width, 46));
        }

        private void AddEnemy(float x, float heightAboveGround, int kind, float health, float speed)
        {
            Enemy z = new Enemy(x, groundY - Player.BodyHeight, kind, health, speed, false);
            if (heightAboveGround > 0)
                z.y = groundY - Player.BodyHeight - heightAboveGround;
            z.face = -1;
            enemies.Add(z);
        }

        private void StartGame()
        {
            ResetGame();
            mode = PlayMode.Playing;
            clock.Restart();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            float dt = (float)Math.Min(clock.Elapsed.TotalSeconds, 0.035);
            clock.Restart();

            if (mode == PlayMode.Playing)
                UpdateGame(dt);
            else
            {
                gameTime += dt * 0.1f;
                if (introFade > 0)
                    introFade = Math.Max(0, introFade - dt * 0.5f);
            }

            Invalidate();
        }

        private void UpdateGame(float dt)
        {
            gameTime += dt;
            groundY = GroundLine;
            if (hero.y + Player.BodyHeight > groundY + 250)
                hero.y = groundY - Player.BodyHeight;

            if (hero.invulnerable > 0) hero.invulnerable -= dt;
            if (hero.fireTimer > 0) hero.fireTimer -= dt;
            if (hero.dashTimer > 0) hero.dashTimer -= dt;
            if (hero.dashCooldown > 0) hero.dashCooldown -= dt;
            if (hero.meleeCooldown > 0) hero.meleeCooldown -= dt;
            if (hero.meleeTimer > 0) hero.meleeTimer -= dt;
            if (introFade > 0) introFade = Math.Max(0, introFade - dt * 0.8f);

            UpdateReload(dt);
            UpdatePlayer(dt);
            UpdateEnemies(dt);
            UpdateBoss(dt);
            UpdateShots(dt);
            UpdatePickups(dt);
            UpdateParticles(dt);
            UpdateCamera(dt);

            if (hero.hp <= 0)
            {
                hero.hp = 0;
                mode = PlayMode.Dead;
                bestScore = Math.Max(bestScore, score);
            }

            if (boss != null && !boss.alive && hero.x >= levelExitX - 120)
            {
                mode = PlayMode.Won;
                bestScore = Math.Max(bestScore, score);
            }

            if (bossWarning > 0) bossWarning -= dt;
            if (bossFlash > 0) bossFlash -= dt;
            if (shakeTimer > 0) shakeTimer -= dt;
        }

        private void UpdatePlayer(float dt)
        {
            float dir = 0;
            if (leftDown) dir -= 1;
            if (rightDown) dir += 1;
            if (dir != 0)
            {
                hero.face = dir > 0 ? 1 : -1;
                hero.animation += dt * 9.5f;
            }
            else
                hero.animation = 0;

            if (hero.dashTimer > 0)
                hero.vx = hero.face * 780;
            else
                hero.vx = dir * WalkSpeed;

            float previousBottom = hero.y + Player.BodyHeight;
            hero.x += hero.vx * dt;
            hero.x = Math.Max(0, Math.Min(WorldWidth - Player.BodyWidth, hero.x));

            hero.vy += 1900 * dt;
            hero.y += hero.vy * dt;
            hero.onGround = false;

            if (hero.y + Player.BodyHeight >= groundY)
            {
                hero.y = groundY - Player.BodyHeight;
                hero.vy = 0;
                hero.onGround = true;
            }

            for (int i = 0; i < platforms.Count; i++)
            {
                Platform p = platforms[i];
                bool overlapsX = hero.x + Player.BodyWidth > p.x && hero.x < p.x + p.width;
                float bottom = hero.y + Player.BodyHeight;
                if (overlapsX && hero.vy >= 0 && previousBottom <= p.top + 10 && bottom >= p.top)
                {
                    hero.y = p.top - Player.BodyHeight;
                    hero.vy = 0;
                    hero.onGround = true;
                }
            }

            if (fireDown || controlFireDown)
                TryShoot();

            if (meleeDown && hero.meleeCooldown <= 0)
            {
                hero.meleeCooldown = 0.72f;
                hero.meleeTimer = 0.22f;
                DoMelee();
            }
        }

        private void UpdateReload(float dt)
        {
            if (!hero.reloading) return;
            hero.reloadTimer -= dt;
            if (hero.reloadTimer <= 0)
            {
                int needed = 12 - hero.ammo;
                int load = Math.Min(needed, hero.reserve);
                hero.ammo += load;
                hero.reserve -= load;
                hero.reloading = false;
                hero.reloadTimer = 0;
            }
        }

        private void TryShoot()
        {
            if (hero.fireTimer > 0 || hero.reloading) return;
            if (hero.ammo <= 0)
            {
                if (hero.reserve > 0)
                    StartReload();
                return;
            }

            hero.ammo--;
            hero.fireTimer = 0.17f;
            shots.Add(new Shot
            {
                x = hero.x + (hero.face > 0 ? Player.BodyWidth : 0),
                y = hero.y + 48,
                vx = hero.face * 1150,
                damage = 34,
                hostile = false
            });
            AddParticles(hero.x + (hero.face > 0 ? 70 : 0), hero.y + 51, Color.FromArgb(255, 232, 139), 4, 110);
        }

        private void StartReload()
        {
            if (hero.reloading || hero.ammo >= 12 || hero.reserve <= 0) return;
            hero.reloading = true;
            hero.reloadTimer = 0.88f;
        }

        private void DoMelee()
        {
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                Enemy e = enemies[i];
                float dx = e.x - hero.x;
                if (Math.Abs(dx) < 105 && (dx == 0 || Math.Sign(dx) == hero.face))
                    DamageEnemy(e, 46);
            }

            if (boss != null && boss.alive)
            {
                float dx = boss.x - hero.x;
                if (Math.Abs(dx) < 145 && Math.Sign(dx) == hero.face)
                    DamageBoss(26);
            }
        }

        private void UpdateEnemies(float dt)
        {
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                Enemy e = enemies[i];
                if (!e.alive)
                {
                    enemies.RemoveAt(i);
                    continue;
                }

                e.animation += dt;
                if (e.attackCooldown > 0) e.attackCooldown -= dt;
                if (e.attackTimer > 0) e.attackTimer -= dt;
                if (e.hurtTimer > 0) e.hurtTimer -= dt;

                float dx = hero.x - e.x;
                float distance = Math.Abs(dx);
                RectangleF playerBox = new RectangleF(hero.x, hero.y, Player.BodyWidth, Player.BodyHeight);
                RectangleF enemyBox = EnemyBox(e);
                bool overlapsVertically = playerBox.Bottom > enemyBox.Top && playerBox.Top < enemyBox.Bottom;
                if (distance < 820 && distance > 60)
                {
                    e.face = dx > 0 ? 1 : -1;
                    e.x += Math.Sign(dx) * e.speed * dt;
                }
                else if (distance <= 65 && overlapsVertically && e.attackCooldown <= 0)
                {
                    e.attackCooldown = e.kind == 2 ? 0.72f : 1.0f;
                    e.attackTimer = 0.44f;
                    DamagePlayer(e.kind == 2 ? 18 : 12, Math.Sign(dx));
                    Shake(4, 0.12f);
                }
            }
        }

        private void UpdateBoss(float dt)
        {
            if (boss == null || !boss.alive) return;
            float dx = hero.x - boss.x;
            float distance = Math.Abs(dx);
            if (hero.x > 4740)
            {
                if (bossWarning <= 0)
                {
                    bossWarning = 1.7f;
                    Shake(7, 0.32f);
                }

                if (distance > 260)
                {
                    boss.face = dx > 0 ? 1 : -1;
                    boss.x += Math.Sign(dx) * boss.speed * dt;
                    boss.x = Math.Max(5010, Math.Min(5600, boss.x));
                }

                bossAttackTimer -= dt;
                if (bossAttackTimer <= 0 && bossWindup <= 0)
                {
                    bossWindup = 0.78f;
                    bossAttackTimer = 2.7f;
                }
                else if (bossWindup > 0)
                {
                    bossWindup -= dt;
                    if (bossWindup <= 0)
                        BossBurst();
                }

                if (distance < 105 && boss.attackCooldown <= 0)
                {
                    boss.attackCooldown = 1.05f;
                    boss.attackTimer = 0.48f;
                    DamagePlayer(22, Math.Sign(dx));
                    Shake(8, 0.18f);
                }
            }

            if (boss.attackCooldown > 0) boss.attackCooldown -= dt;
            if (boss.attackTimer > 0) boss.attackTimer -= dt;
            if (boss.hurtTimer > 0) boss.hurtTimer -= dt;
            boss.animation += dt;
        }

        private void BossBurst()
        {
            float originX = boss.x + 42;
            float originY = boss.y + 72;
            float targetX = hero.x + Player.BodyWidth / 2;
            float targetY = hero.y + Player.BodyHeight / 2;
            float dx = targetX - originX;
            float dy = targetY - originY;
            float length = Math.Max(1, (float)Math.Sqrt(dx * dx + dy * dy));
            float speed = 390;
            for (int i = -1; i <= 1; i++)
            {
                shots.Add(new Shot
                {
                    x = originX,
                    y = originY,
                    vx = (dx / length + i * 0.16f) * speed,
                    life = 3.2f,
                    damage = 17,
                    hostile = true
                });
            }
            AddParticles(originX, originY, Color.FromArgb(150, 255, 89), 12, 160);
            Shake(5, 0.16f);
        }

        private void UpdateShots(float dt)
        {
            for (int i = shots.Count - 1; i >= 0; i--)
            {
                Shot s = shots[i];
                s.x += s.vx * dt;
                s.y += s.hostile ? 130 * dt : 0;
                s.life -= dt;
                if (s.life <= 0 || s.x < cameraX - 150 || s.x > cameraX + ClientSize.Width + 180)
                {
                    shots.RemoveAt(i);
                    continue;
                }

                RectangleF shotBox = new RectangleF(s.x - 8, s.y - 5, 18, 10);
                if (!s.hostile)
                {
                    bool used = false;
                    for (int j = enemies.Count - 1; j >= 0; j--)
                    {
                        Enemy e = enemies[j];
                        RectangleF enemyBox = EnemyBox(e);
                        if (enemyBox.IntersectsWith(shotBox))
                        {
                            DamageEnemy(e, s.damage);
                            shots.RemoveAt(i);
                            used = true;
                            break;
                        }
                    }
                    if (!used && boss != null && boss.alive && EnemyBox(boss).IntersectsWith(shotBox))
                    {
                        DamageBoss(s.damage);
                        shots.RemoveAt(i);
                    }
                }
                else if (new RectangleF(hero.x, hero.y, Player.BodyWidth, Player.BodyHeight).IntersectsWith(shotBox))
                {
                    DamagePlayer(s.damage, s.vx < 0 ? -1 : 1);
                    shots.RemoveAt(i);
                }
            }
        }

        private RectangleF EnemyBox(Enemy e)
        {
            if (e.isBoss) return new RectangleF(e.x + 12, e.y + 12, 125, 160);
            if (e.kind == 0) return new RectangleF(e.x + 12, e.y + 12, 58, 96);
            return new RectangleF(e.x + 15, e.y + 12, 52, 112);
        }

        private void DamageEnemy(Enemy e, int amount)
        {
            e.hp -= amount;
            e.hurtTimer = 0.14f;
            AddParticles(e.x + 40, e.y + 50, Color.FromArgb(159, 255, 113), 5, 130);
            if (e.hp <= 0)
            {
                e.alive = false;
                score += 120;
                kills++;
                AddParticles(e.x + 40, e.y + 60, Color.FromArgb(103, 205, 111), 18, 240);
                if (kills % 3 == 0)
                    pickups.Add(new Pickup(e.x + 12, groundY - 44, random.Next(0, 2)));
            }
        }

        private void DamageBoss(int amount)
        {
            if (boss == null || !boss.alive) return;
            boss.hp -= amount;
            boss.hurtTimer = 0.14f;
            bossFlash = 0.14f;
            AddParticles(boss.x + 76, boss.y + 80, Color.FromArgb(185, 250, 113), 7, 150);
            if (boss.hp <= 0)
            {
                boss.hp = 0;
                boss.alive = false;
                score += 1200;
                AddParticles(boss.x + 80, boss.y + 80, Color.FromArgb(161, 241, 110), 46, 320);
                Shake(13, 0.48f);
            }
        }

        private void DamagePlayer(int amount, int knockDirection)
        {
            if (hero.invulnerable > 0 || mode != PlayMode.Playing) return;
            hero.hp -= amount;
            hero.invulnerable = 0.85f;
            hero.vx = knockDirection * 245;
            hero.vy = -270;
            hero.onGround = false;
            AddParticles(hero.x + 28, hero.y + 48, Color.FromArgb(255, 89, 82), 10, 180);
        }

        private void UpdatePickups(float dt)
        {
            for (int i = 0; i < pickups.Count; i++)
            {
                Pickup p = pickups[i];
                if (p.taken) continue;
                p.bob += dt * 3.3f;
                RectangleF pickupBox = new RectangleF(p.x - 18, p.y - 18 + (float)Math.Sin(p.bob) * 5, 38, 40);
                if (pickupBox.IntersectsWith(new RectangleF(hero.x, hero.y, Player.BodyWidth, Player.BodyHeight)))
                {
                    p.taken = true;
                    if (p.kind == 0)
                    {
                        hero.hp = Math.Min(100, hero.hp + 32);
                        AddParticles(p.x, p.y, Color.FromArgb(108, 255, 162), 16, 190);
                    }
                    else
                    {
                        hero.reserve = Math.Min(120, hero.reserve + 30);
                        hero.ammo = Math.Min(12, hero.ammo + 6);
                        AddParticles(p.x, p.y, Color.FromArgb(255, 208, 98), 16, 190);
                    }
                    score += 45;
                }
            }
        }

        private void UpdateParticles(float dt)
        {
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                Particle p = particles[i];
                p.x += p.vx * dt;
                p.y += p.vy * dt;
                p.vy += 280 * dt;
                p.life -= dt;
                if (p.life <= 0) particles.RemoveAt(i);
            }
        }

        private void AddParticles(float x, float y, Color color, int amount, float speed)
        {
            for (int i = 0; i < amount; i++)
            {
                double angle = random.NextDouble() * Math.PI * 2;
                float force = (float)(random.NextDouble() * speed);
                particles.Add(new Particle
                {
                    x = x,
                    y = y,
                    vx = (float)Math.Cos(angle) * force,
                    vy = (float)Math.Sin(angle) * force,
                    life = 0.22f + (float)random.NextDouble() * 0.52f,
                    size = 2 + (float)random.NextDouble() * 4,
                    color = color
                });
            }
        }

        private void UpdateCamera(float dt)
        {
            float target = hero.x - ClientSize.Width * 0.38f;
            target = Math.Max(0, Math.Min(WorldWidth - ClientSize.Width, target));
            cameraX += (target - cameraX) * Math.Min(1, dt * 7.5f);
        }

        private void Shake(float power, float time)
        {
            shakePower = power;
            shakeTimer = Math.Max(shakeTimer, time);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            DrawBackground(g);

            float shakeX = 0;
            float shakeY = 0;
            if (shakeTimer > 0)
            {
                shakeX = (float)(random.NextDouble() * shakePower - shakePower / 2);
                shakeY = (float)(random.NextDouble() * shakePower - shakePower / 2);
            }

            GraphicsState state = g.Save();
            g.TranslateTransform(-cameraX + shakeX, shakeY);
            DrawWorld(g);
            DrawPickups(g);
            DrawEnemies(g);
            DrawBoss(g);
            DrawShots(g);
            DrawPlayer(g);
            DrawParticles(g);
            g.Restore(state);

            DrawHud(g);
            DrawOverlay(g);
        }

        private void DrawBackground(Graphics g)
        {
            Rectangle r = ClientRectangle;
            using (LinearGradientBrush sky = new LinearGradientBrush(r, Color.FromArgb(13, 20, 34), Color.FromArgb(35, 47, 58), 90f))
                g.FillRectangle(sky, r);

            using (Brush moon = new SolidBrush(Color.FromArgb(52, 195, 213, 181)))
            {
                float mx = ClientSize.Width * 0.76f - cameraX * 0.04f;
                g.FillEllipse(moon, mx, ClientSize.Height * 0.13f, 86, 86);
            }

            float baseY = GroundLine;
            float offset = -(cameraX * 0.19f) % 190;
            for (int i = -2; i < skylineHeights.Count; i++)
            {
                int index = (i + skylineHeights.Count) % skylineHeights.Count;
                float x = i * 190 + offset;
                float h = skylineHeights[index];
                Color buildingColor = index % 3 == 0 ? Color.FromArgb(27, 38, 48) : Color.FromArgb(23, 33, 45);
                using (Brush building = new SolidBrush(buildingColor))
                    g.FillRectangle(building, x, baseY - h - 70, 166, h + 70);

                for (int wy = 0; wy < h / 32; wy++)
                {
                    for (int wx = 0; wx < 3; wx++)
                    {
                        if ((index * 7 + wy * 3 + wx) % 4 == 0)
                        {
                            Color light = (index + wx) % 2 == 0 ? Color.FromArgb(130, 187, 153, 92) : Color.FromArgb(105, 111, 171, 147);
                            using (Brush window = new SolidBrush(light))
                                g.FillRectangle(window, x + 22 + wx * 42, baseY - 55 - wy * 31, 9, 13);
                        }
                    }
                }
            }

            using (Brush haze = new SolidBrush(Color.FromArgb(24, 142, 180, 145)))
                g.FillRectangle(haze, 0, baseY - 136, ClientSize.Width, 58);
        }

        private void DrawWorld(Graphics g)
        {
            float floor = GroundLine;
            using (Brush ground = new SolidBrush(Color.FromArgb(25, 32, 41)))
                g.FillRectangle(ground, 0, floor, WorldWidth, ClientSize.Height - floor + 20);
            using (Pen top = new Pen(Color.FromArgb(103, 159, 126), 6))
                g.DrawLine(top, 0, floor, WorldWidth, floor);
            using (Pen edge = new Pen(Color.FromArgb(42, 75, 67), 2))
                g.DrawLine(edge, 0, floor + 11, WorldWidth, floor + 11);

            Bitmap tile = Img("World/brickGrey.png");
            for (float x = 0; x < WorldWidth; x += 78)
            {
                RectangleF dest = new RectangleF(x, floor + 22, 78, 62);
                g.DrawImage(tile, dest);
                using (Brush shade = new SolidBrush(Color.FromArgb(155, 11, 19, 27)))
                    g.FillRectangle(shade, dest);
            }

            for (int i = 0; i < platforms.Count; i++)
                DrawPlatform(g, platforms[i]);

            DrawCrate(g, 1980, floor - 70);
            DrawCrate(g, 2020, floor - 70);
            DrawCrate(g, 4370, floor - 70);
            DrawCrate(g, 4410, floor - 70);
            DrawExit(g);
        }

        private void DrawPlatform(Graphics g, Platform p)
        {
            Bitmap tile = Img("World/brickGrey.png");
            for (float x = p.x; x < p.x + p.width; x += 68)
            {
                RectangleF dest = new RectangleF(x, p.top, Math.Min(68, p.x + p.width - x), p.height);
                g.DrawImage(tile, dest);
                using (Brush shade = new SolidBrush(Color.FromArgb(126, 12, 21, 30)))
                    g.FillRectangle(shade, dest);
            }
            using (Pen trim = new Pen(Color.FromArgb(96, 190, 135), 4))
                g.DrawLine(trim, p.x, p.top + 2, p.x + p.width, p.top + 2);
        }

        private void DrawCrate(Graphics g, float x, float y)
        {
            Bitmap crate = Img("World/boxCrate.png");
            g.DrawImage(crate, x, y, 68, 68);
            using (Pen outline = new Pen(Color.FromArgb(90, 14, 20, 24), 2))
                g.DrawRectangle(outline, x + 1, y + 1, 66, 66);
        }

        private void DrawExit(Graphics g)
        {
            bool open = boss != null && !boss.alive;
            Bitmap top = Img(open ? "World/doorOpen_top.png" : "World/doorOpen_top.png");
            Bitmap mid = Img("World/doorOpen_mid.png");
            float x = levelExitX;
            using (Brush frame = new SolidBrush(Color.FromArgb(23, 34, 45)))
                g.FillRectangle(frame, x, GroundLine - 186, 92, 186);
            g.DrawImage(top, x - 9, GroundLine - 194, 110, 78);
            g.DrawImage(mid, x + 4, GroundLine - 122, 82, 112);
            using (Brush glow = new SolidBrush(Color.FromArgb(open ? 85 : 32, open ? 112 : 76, open ? 226 : 110, open ? 157 : 110)))
                g.FillRectangle(glow, x + 24, GroundLine - 102, 40, 91);
            using (Font f = new Font("Arial", 9, FontStyle.Bold))
                g.DrawString(open ? "EXIT" : "LOCKED", f, Brushes.White, x + 20, GroundLine - 179);
        }

        private void DrawPickups(Graphics g)
        {
            for (int i = 0; i < pickups.Count; i++)
            {
                Pickup p = pickups[i];
                if (p.taken) continue;
                float bob = (float)Math.Sin(p.bob) * 5;
                float x = p.x;
                float y = p.y + bob;
                using (Brush halo = new SolidBrush(Color.FromArgb(48, p.kind == 0 ? 103 : 255, p.kind == 0 ? 249 : 202, p.kind == 0 ? 163 : 90)))
                    g.FillEllipse(halo, x - 20, y - 21, 42, 42);
                if (p.kind == 0)
                {
                    using (Brush red = new SolidBrush(Color.FromArgb(218, 75, 76)))
                        g.FillRectangle(red, x - 12, y - 14, 24, 28);
                    using (Brush white = new SolidBrush(Color.FromArgb(242, 242, 228)))
                    {
                        g.FillRectangle(white, x - 4, y - 10, 8, 20);
                        g.FillRectangle(white, x - 10, y - 4, 20, 8);
                    }
                }
                else
                {
                    g.DrawImage(Img("World/boxCoin.png"), x - 15, y - 15, 30, 30);
                }
            }
        }

        private void DrawEnemies(Graphics g)
        {
            for (int i = 0; i < enemies.Count; i++)
                DrawEnemy(g, enemies[i]);
        }

        private void DrawEnemy(Graphics g, Enemy e)
        {
            bool attacking = e.attackTimer > 0.17f;
            if (e.kind == 0)
            {
                Bitmap sprite;
                if (e.hurtTimer > 0) sprite = Img("Zombies/hurt.png");
                else if (attacking) sprite = Img(((int)(e.animation * 10) % 2 == 0) ? "Zombies/attack1.png" : "Zombies/attack2.png");
                else sprite = Img(((int)(e.animation * 7) % 2 == 0) ? "Zombies/walk1.png" : "Zombies/walk2.png");
                DrawSprite(g, sprite, e.x, e.y, 82, 113, e.face);
            }
            else
            {
                Bitmap sheet;
                int frames;
                if (attacking && e.kind == 1)
                {
                    sheet = toxicAttackA;
                    frames = 5;
                }
                else if (e.kind == 1)
                {
                    sheet = toxicWalkA;
                    frames = 4;
                }
                else if (e.kind == 2)
                {
                    sheet = toxicWalkB;
                    frames = 4;
                }
                else
                {
                    sheet = toxicWalkC;
                    frames = 4;
                }

                int frame = ((int)(e.animation * 7)) % frames;
                float w = e.kind == 1 ? 76 : 82;
                float h = e.kind == 1 ? 111 : 108;
                DrawSheetFrame(g, sheet, frame, frames, e.x, e.y, w, h, e.face);
                if (e.hurtTimer > 0)
                    using (Brush flash = new SolidBrush(Color.FromArgb(70, 255, 255, 255)))
                        g.FillEllipse(flash, e.x + 16, e.y + 18, w - 28, h - 24);
            }

            if (e.hp < e.maxhp)
                DrawSmallHealth(g, e.x + 5, e.y - 12, 66, e.hp, e.maxhp);
        }

        private void DrawBoss(Graphics g)
        {
            if (boss == null || !boss.alive) return;
            bool warning = bossWindup > 0;
            if (warning)
            {
                using (Pen telegraph = new Pen(Color.FromArgb(140 + (int)(Math.Sin(gameTime * 28) * 50), 255, 81, 78), 4))
                    g.DrawEllipse(telegraph, boss.x - 14, boss.y - 14, 162, 188);
            }

            Bitmap sheet = warning ? toxicAttackA : toxicWalkC;
            int frames = warning ? 5 : 4;
            int frame = ((int)(boss.animation * (warning ? 8 : 4))) % frames;
            DrawSheetFrame(g, sheet, frame, frames, boss.x, boss.y, 165, 200, boss.face);
            using (Pen aura = new Pen(Color.FromArgb(65, 161, 242, 90), 9))
                g.DrawEllipse(aura, boss.x + 19, boss.y + 19, 126, 170);
            DrawSmallHealth(g, boss.x + 22, boss.y - 12, 118, boss.hp, boss.maxhp);
        }

        private void DrawPlayer(Graphics g)
        {
            if (hero.invulnerable > 0 && ((int)(hero.invulnerable * 18) % 2 == 0))
                return;

            Bitmap sprite;
            if (hero.invulnerable > 0.62f) sprite = Img("Player/hurt.png");
            else if (hero.meleeTimer > 0) sprite = Img("Player/melee.png");
            else if (hero.fireTimer > 0.10f) sprite = Img("Player/shoot.png");
            else if (!hero.onGround) sprite = Img("Player/jump.png");
            else if (leftDown || rightDown) sprite = Img(((int)(hero.animation * 1.2f) % 2 == 0) ? "Player/walk1.png" : "Player/walk2.png");
            else sprite = Img("Player/stand.png");

            DrawSprite(g, sprite, hero.x - 12, hero.y - 7, PlayerDrawWidth, PlayerDrawHeight, hero.face);
            if (hero.dashTimer > 0)
            {
                using (Pen trail = new Pen(Color.FromArgb(125, 163, 245, 160), 7))
                    g.DrawLine(trail, hero.x + 28, hero.y + 74, hero.x - hero.face * 56, hero.y + 74);
            }
        }

        private void DrawShots(Graphics g)
        {
            for (int i = 0; i < shots.Count; i++)
            {
                Shot s = shots[i];
                Color color = s.hostile ? Color.FromArgb(178, 255, 83) : Color.FromArgb(255, 205, 103);
                using (Brush glow = new SolidBrush(Color.FromArgb(95, color)))
                    g.FillEllipse(glow, s.x - 10, s.y - 10, 21, 21);
                using (Pen line = new Pen(color, s.hostile ? 5 : 4))
                    g.DrawLine(line, s.x - (s.vx > 0 ? 15 : -15), s.y, s.x, s.y);
            }
        }

        private void DrawParticles(Graphics g)
        {
            for (int i = 0; i < particles.Count; i++)
            {
                Particle p = particles[i];
                int alpha = (int)(255 * Math.Max(0, Math.Min(1, p.life / 0.7f)));
                using (Brush b = new SolidBrush(Color.FromArgb(alpha, p.color)))
                    g.FillEllipse(b, p.x, p.y, p.size, p.size);
            }
        }

        private void DrawSmallHealth(Graphics g, float x, float y, float width, float hp, float maxHp)
        {
            using (Brush bg = new SolidBrush(Color.FromArgb(210, 20, 23, 29)))
                g.FillRectangle(bg, x, y, width, 8);
            using (Brush fill = new SolidBrush(Color.FromArgb(231, 74, 74)))
                g.FillRectangle(fill, x + 1, y + 1, Math.Max(0, (width - 2) * hp / maxHp), 6);
        }

        private void DrawHud(Graphics g)
        {
            using (Brush panel = new SolidBrush(Color.FromArgb(212, 13, 19, 27)))
                g.FillRectangle(panel, 22, 20, 328, 110);
            using (Pen border = new Pen(Color.FromArgb(108, 146, 126), 2))
                g.DrawRectangle(border, 22, 20, 328, 110);
            using (Font label = new Font("Arial", 9, FontStyle.Bold))
            using (Font number = new Font("Arial", 17, FontStyle.Bold))
            {
                g.DrawString("VITALS", label, new SolidBrush(Color.FromArgb(152, 174, 166)), 39, 32);
                g.DrawString(hero.hp.ToString("000"), number, Brushes.White, 39, 50);
                g.DrawString("AMMO", label, new SolidBrush(Color.FromArgb(152, 174, 166)), 146, 32);
                g.DrawString(hero.ammo.ToString("00") + " / " + hero.reserve.ToString("00"), number, Brushes.White, 146, 50);
                g.DrawString("SCORE  " + score.ToString("000000"), label, Brushes.White, 39, 98);
                g.DrawString("KILLS  " + kills.ToString("00"), label, Brushes.White, 220, 98);
            }
            using (Brush hpBack = new SolidBrush(Color.FromArgb(70, 255, 255, 255)))
                g.FillRectangle(hpBack, 39, 82, 104, 8);
            using (Brush hpFill = new SolidBrush(Color.FromArgb(96, 218, 130)))
                g.FillRectangle(hpFill, 39, 82, Math.Max(0, 104 * hero.hp / 100), 8);
            using (Brush ammoBack = new SolidBrush(Color.FromArgb(70, 255, 255, 255)))
                g.FillRectangle(ammoBack, 146, 82, 132, 8);
            using (Brush ammoFill = new SolidBrush(Color.FromArgb(242, 197, 86)))
                g.FillRectangle(ammoFill, 146, 82, 132 * hero.ammo / 12, 8);

            using (Font objective = new Font("Arial", 10, FontStyle.Bold))
                g.DrawString(boss != null && boss.alive ? "OBJECTIVE   REACH THE NEST" : "OBJECTIVE   REACH THE EXIT", objective, Brushes.White, ClientSize.Width - 310, 26);

            if (hero.reloading)
            {
                using (Font f = new Font("Arial", 11, FontStyle.Bold))
                    g.DrawString("RELOADING", f, new SolidBrush(Color.FromArgb(255, 220, 123)), 40, 139);
            }

            if (boss != null && boss.alive && hero.x > 4600)
            {
                using (Font title = new Font("Arial", 10, FontStyle.Bold))
                    g.DrawString("THE WARDEN", title, Brushes.White, ClientSize.Width / 2 - 55, 35);
                using (Brush bg = new SolidBrush(Color.FromArgb(198, 20, 22, 29)))
                    g.FillRectangle(bg, ClientSize.Width / 2 - 205, 56, 410, 17);
                using (Brush fill = new SolidBrush(Color.FromArgb(228, 83, 70)))
                    g.FillRectangle(fill, ClientSize.Width / 2 - 202, 59, 404 * boss.hp / boss.maxhp, 11);
            }
        }

        private void DrawOverlay(Graphics g)
        {
            if (mode == PlayMode.Title)
            {
                using (Brush shade = new SolidBrush(Color.FromArgb(110, 5, 10, 17)))
                    g.FillRectangle(shade, ClientRectangle);
                DrawTitleCard(g);
            }
            else if (mode == PlayMode.Paused)
            {
                DrawCenterOverlay(g, "PAUSED", "Press P or Esc to return", "R  RESTART RUN");
            }
            else if (mode == PlayMode.Dead)
            {
                DrawCenterOverlay(g, "YOU WERE OVERRUN", "Score  " + score.ToString("000000") + "     Kills  " + kills.ToString("00"), "ENTER  TRY AGAIN      ESC  TITLE");
            }
            else if (mode == PlayMode.Won)
            {
                DrawCenterOverlay(g, "THE NEST IS SILENT", "You escaped with " + score.ToString("000000") + " points", "ENTER  PLAY AGAIN      ESC  TITLE");
            }
        }

        private void DrawTitleCard(Graphics g)
        {
            float cardW = Math.Min(820, ClientSize.Width - 70);
            float cardX = (ClientSize.Width - cardW) / 2;
            float cardY = ClientSize.Height * 0.12f;
            using (Brush panel = new SolidBrush(Color.FromArgb(224, 12, 19, 27)))
                g.FillRectangle(panel, cardX, cardY, cardW, ClientSize.Height * 0.69f);
            using (Pen outline = new Pen(Color.FromArgb(130, 122, 179, 147), 2))
                g.DrawRectangle(outline, cardX, cardY, cardW, ClientSize.Height * 0.69f);

            using (Font small = new Font("Arial", 10, FontStyle.Bold))
            using (Font title = new Font("Arial", Math.Min(42, ClientSize.Width / 23), FontStyle.Bold))
            using (Font sub = new Font("Arial", 12, FontStyle.Bold))
            using (Font body = new Font("Arial", 11, FontStyle.Regular))
            {
                g.DrawString("A SIDE-SCROLLING SURVIVAL SHOOTER", small, new SolidBrush(Color.FromArgb(140, 217, 170)), cardX + 42, cardY + 35);
                g.DrawString("LONG GAME", title, Brushes.White, cardX + 38, cardY + 65);
                g.DrawString("LAST STAND", title, new SolidBrush(Color.FromArgb(186, 230, 130)), cardX + 38, cardY + 111);
                g.DrawString("The quarantine line has fallen. Reach the nest, bring down the Warden, and get out.", body, new SolidBrush(Color.FromArgb(210, 221, 219)), cardX + 42, cardY + 182);
                g.DrawString("MOVE   A / D  or  ARROWS       JUMP   SPACE / W / UP", sub, Brushes.White, cardX + 42, cardY + 238);
                g.DrawString("FIRE   MOUSE  or  CTRL              RELOAD   R", sub, Brushes.White, cardX + 42, cardY + 277);
                g.DrawString("DASH   SHIFT       MELEE   Q       PAUSE   P / ESC", sub, Brushes.White, cardX + 42, cardY + 316);
                g.DrawString("Collect med kits and ammo. The Warden becomes vulnerable when you reach the nest.", body, new SolidBrush(Color.FromArgb(188, 203, 200)), cardX + 42, cardY + 369);
                g.DrawString("ENTER  START RUN", title, new SolidBrush(Color.FromArgb(255, 215, 115)), cardX + 42, cardY + 433);
                g.DrawString("BEST RUN   " + bestScore.ToString("000000"), small, new SolidBrush(Color.FromArgb(150, 171, 163)), cardX + 44, cardY + 505);
            }
        }

        private void DrawCenterOverlay(Graphics g, string title, string middle, string bottom)
        {
            using (Brush shade = new SolidBrush(Color.FromArgb(164, 5, 9, 15)))
                g.FillRectangle(shade, ClientRectangle);
            float w = Math.Min(620, ClientSize.Width - 60);
            float x = (ClientSize.Width - w) / 2;
            float y = ClientSize.Height * 0.34f;
            using (Brush panel = new SolidBrush(Color.FromArgb(232, 12, 18, 26)))
                g.FillRectangle(panel, x, y, w, 235);
            using (Pen edge = new Pen(Color.FromArgb(125, 150, 185, 148), 2))
                g.DrawRectangle(edge, x, y, w, 235);
            using (Font heading = new Font("Arial", 28, FontStyle.Bold))
            using (Font body = new Font("Arial", 12, FontStyle.Regular))
            using (Font controls = new Font("Arial", 10, FontStyle.Bold))
            {
                g.DrawString(title, heading, Brushes.White, x + 34, y + 32);
                g.DrawString(middle, body, new SolidBrush(Color.FromArgb(205, 218, 213)), x + 38, y + 101);
                g.DrawString(bottom, controls, new SolidBrush(Color.FromArgb(255, 213, 116)), x + 38, y + 164);
            }
        }

        private void DrawSprite(Graphics g, Bitmap sprite, float x, float y, float width, float height, int direction)
        {
            GraphicsState state = g.Save();
            if (direction < 0)
            {
                g.TranslateTransform(x + width, y);
                g.ScaleTransform(-1, 1);
                g.DrawImage(sprite, 0, 0, width, height);
            }
            else
                g.DrawImage(sprite, x, y, width, height);
            g.Restore(state);
        }

        private void DrawSheetFrame(Graphics g, Bitmap sheet, int frame, int frameCount, float x, float y, float width, float height, int direction)
        {
            int frameWidth = Math.Max(1, sheet.Width / frameCount);
            int sx = frame * frameWidth;
            int sw = frame == frameCount - 1 ? sheet.Width - sx : frameWidth;
            GraphicsState state = g.Save();
            if (direction < 0)
            {
                g.TranslateTransform(x + width, y);
                g.ScaleTransform(-1, 1);
                g.DrawImage(sheet, new RectangleF(0, 0, width, height), new RectangleF(sx, 0, sw, sheet.Height), GraphicsUnit.Pixel);
            }
            else
                g.DrawImage(sheet, new RectangleF(x, y, width, height), new RectangleF(sx, 0, sw, sheet.Height), GraphicsUnit.Pixel);
            g.Restore(state);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Enter)
            {
                if (mode == PlayMode.Title || mode == PlayMode.Dead || mode == PlayMode.Won)
                    StartGame();
                return;
            }

            if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.P)
            {
                if (mode == PlayMode.Playing) mode = PlayMode.Paused;
                else if (mode == PlayMode.Paused) mode = PlayMode.Playing;
                else if (mode == PlayMode.Dead || mode == PlayMode.Won) mode = PlayMode.Title;
                return;
            }

            if (e.KeyCode == Keys.R && mode == PlayMode.Playing)
            {
                if (hero.reloading) return;
                StartReload();
                return;
            }

            if (e.KeyCode == Keys.Left || e.KeyCode == Keys.A) leftDown = true;
            if (e.KeyCode == Keys.Right || e.KeyCode == Keys.D) rightDown = true;
            if (e.KeyCode == Keys.ControlKey) controlFireDown = true;
            if (e.KeyCode == Keys.Q) meleeDown = true;

            if ((e.KeyCode == Keys.Space || e.KeyCode == Keys.W || e.KeyCode == Keys.Up) && !jumpDown)
            {
                jumpDown = true;
                if (mode == PlayMode.Playing && hero.onGround)
                {
                    hero.vy = -1090;
                    hero.onGround = false;
                }
            }

            if (e.KeyCode == Keys.ShiftKey && mode == PlayMode.Playing && hero.dashCooldown <= 0)
            {
                hero.dashTimer = 0.20f;
                hero.dashCooldown = 2.6f;
                hero.invulnerable = Math.Max(hero.invulnerable, 0.28f);
                AddParticles(hero.x + 25, hero.y + 70, Color.FromArgb(131, 228, 175), 14, 210);
            }

            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.W || e.KeyCode == Keys.Up || e.KeyCode == Keys.Left || e.KeyCode == Keys.Right)
                e.SuppressKeyPress = true;
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);
            if (e.KeyCode == Keys.Left || e.KeyCode == Keys.A) leftDown = false;
            if (e.KeyCode == Keys.Right || e.KeyCode == Keys.D) rightDown = false;
            if (e.KeyCode == Keys.ControlKey) controlFireDown = false;
            if (e.KeyCode == Keys.Q) meleeDown = false;
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.W || e.KeyCode == Keys.Up) jumpDown = false;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                fireDown = true;
                if (mode == PlayMode.Title) StartGame();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left) fireDown = false;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            timer.Stop();
            foreach (Bitmap bitmap in images.Values) bitmap.Dispose();
            toxicWalkA.Dispose();
            toxicAttackA.Dispose();
            toxicWalkB.Dispose();
            toxicWalkC.Dispose();
            base.OnFormClosed(e);
        }
    }
}
