using Raylib_cs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

// On retire "using System.Drawing;" pour éviter le conflit global
// Et on crée des alias spécifiques pour Raylib :
using Rectangle = Raylib_cs.Rectangle;
using Color = Raylib_cs.Color;
using System.Runtime.CompilerServices;

namespace RaylibTest
{
    public class Stalactite
    {
        public Rectangle Hitbox;       // La vraie boîte qui fait mal
        public Rectangle DetectionZone; // La grande zone fine en dessous qui déclenche la chute
        public bool IsFalling = false;  // Est-ce qu'elle tombe ?
        public bool IsDestroyed = false;// Est-ce qu'elle a touché le sol/joueur et a disparu ?
        public float FallSpeed = 0f;    // Sa vitesse de chute actuelle
        public float SpawnY;           // Stocke la position Y initiale au plafond
        public float RespawnTimer = 0f; // Le compteur de temps actuel
        public float RespawnDelay = 3f; // Le temps d'attente en secondes 
        public string Type = "glace";
    }
    public class Slime
    {
        public Rectangle body;
        public Color Color;
        public bool isAllied = false;

        public List<string> dialogue = new List<string>();

    }

    public class Death
    {

        public Rectangle Hitbox;
        public bool isVoid = false;

    }
    internal class Program
    {
        static void Main(string[] args)
        {
            bool devMode = false;

            Raylib.SetConfigFlags(ConfigFlags.FullscreenMode);

            Raylib.InitWindow(0, 0, "Slime Thing");
            Raylib.SetTargetFPS(60);
            Vector2 playerpos = new Vector2(325, 300);
            Vector2 lastSafePosition = playerpos;
            float cameraSmoothing = 5.0f;

            // Obtenir la résolution dynamique de l'écran (puisque tu es en Fullscreen)
            int screenWidth = Raylib.GetScreenWidth();
            int screenHeight = Raylib.GetScreenHeight();

            // 1. Texture cible pour capturer le jeu entier
            RenderTexture2D gameTarget = Raylib.LoadRenderTexture(screenWidth, screenHeight);

            // 2. Chargement du Shader
            Shader blurShader = Raylib.LoadShader(null, "blur.fs");

            // On passe la taille de l'écran au shader
            int renderSizeLoc = Raylib.GetShaderLocation(blurShader, "renderSize");
            Vector2 renderSize = new Vector2(screenWidth, screenHeight);
            Raylib.SetShaderValue(blurShader, renderSizeLoc, renderSize, ShaderUniformDataType.Vec2);

            int lightSize = 600;
            RenderTexture2D lightMask = Raylib.LoadRenderTexture(lightSize, lightSize);

            Raylib.BeginTextureMode(lightMask);
            // 1. On nettoie la texture en la rendant totalement transparente
            Raylib.ClearBackground(Color.Blank);

            // 2. On dessine un cercle)
            Raylib.BeginBlendMode(BlendMode.Alpha);
            Raylib.DrawCircleGradient(
                lightSize / 2,
                lightSize / 2,
                lightSize / 2,
                new Color(0, 0, 0, 0),      // Centre : Transparent
                new Color(0, 0, 0, 255)     // Bords : Obscurité
            );
            Raylib.EndBlendMode();
            Raylib.EndTextureMode();
            bool isPlaying = false;
            bool isGrounded = false;
            float gravity = 900;
            float downspeed = 0;
            float currentSpeedX = 0f;      // La vitesse horizontale actuelle du joueur
            float acceleration = 10000f;    // Vitesse à laquelle le joueur accélère
            float normalFriction = 1500f;  // Freinage sur sol normal
            float iceFriction = 100f;      // Freinage sur la glace (beaucoup plus faible !)
            float maxSpeedX = 250f;        // Vitesse maximale du joueur(sur terre)
            float coyoteTimeCounter = 0;
            float coyoteTimeDuration = 0.15f;
            float jumpBufferCounter = 0;
            float jumpBufferDuration = 0.2f;
            // --- VARIABLES POUR LE WALL JUMP ---
            bool isTouchingWallLeft = false;
            bool isTouchingWallRight = false;
            float wallJumpForceY = -500f; // Force verticale du saut
            float wallJumpForceX = 400f;  // Force horizontale de répulsion
            float wallJumpLockTimer = 0f;
            float maxWallSlideSpeed = 150f;

            bool canSwim = false;
            bool canWallJump = false;         // Bloque ou autorise le walljump
            bool isInDialogue = false;        // Met le jeu en "pause" pendant que le slime parle
            int dialogueStep = -1;            // Étape actuelle du dialogue
            Slime? currentTalkingSlime = null; // avec quel slime on parle  

            float waterTime = 5f;
            bool isInWater = false;

            bool canDoubleJumpAndDash = false;
            int doubleJumpCounter = 0;

            bool canDash = true;                   // Est-ce-Que le dash a reset (retour au sol ou contre un mur)
            bool isDashing = false;         // Est-on en train de dasher MAINTENANT ?

            float dashSpeed = 600f;        // Vitesse pendant le dash (très rapide)
            float dashDuration = 0.3f;     // Durée du dash en secondes
            float dashTimer = 0f;           // Timer interne pour la durée restante

            float dashCooldown = 0.6f;      // Temps d'attente entre 2 dashs
            float dashCooldownTimer = 0f;   // Timer de cooldown


            List<Slime> slimes = new List<Slime>
            {

            };

            //initialisation des dialogues et des slimes
            List<string> walljumpDialogue = new List<string>
            {
                "Slime: Merci de m'avoir liberé ! Ces monstres m'ont enfermé dans cette prison de glace, je ne pouvais rien faire sans geler.",
                "Slime: Laisse-moi t'aider. Je ne peux pas te laisser te battre seul contre cet ennemi commun.",
                "Slime: Tu es le dernier espoir d'un monde sans tyrannie...",
                "Systeme: Walljump debloqué !"
            };

            List<string> AirDialogue = new List<string>
            {
                "Slime: Halte humain, ne t'approche pas.",
                "Joueur: Je ne vous veux aucun mal, regardez j'ai déjà un de vos semblables comme ami.",
                "Slime: Serais-tu le héros de la légende ? Celui qui unira les peuples ?",
                "Systeme: Nage debloquée !"
            };


            // Création du slime
            Slime slimeW = new Slime();
            slimeW.dialogue = walljumpDialogue; // On associe la liste directement
            slimeW.body = new Rectangle(3450, 825, 50, 50);
            slimeW.Color = new Color(101, 255, 0, 255);
            slimes.Add(slimeW);

            Slime slimeA = new Slime();
            slimeA.dialogue = AirDialogue; // On associe la liste directement
            slimeA.body = new Rectangle(2000, -1850, 50, 50);
            slimeA.Color = new Color(255, 255, 255, 100);
            slimes.Add(slimeA);

            // Liste de toutes les plateformes
            List<Rectangle> platforms = new List<Rectangle>
            {
                new Rectangle(0, 400, 1000, 100),
                new Rectangle(800, 200, 200, 200),
                new Rectangle(500, 275, 200, 125),
                new Rectangle(1000,250,400,250),
                new Rectangle(1600,250,400,250),
                new Rectangle(2000,475,1200,25),
                new Rectangle(3200,250,2000,250),
                new Rectangle(1750,50,200,25),
                new Rectangle(450,-300,1325,200),
                new Rectangle(1925,-300,1675,200),
                new Rectangle(2100,-100, 1000,225),
                new Rectangle(1375,650,250,25),
                new Rectangle(1125,775,750,100),
                new Rectangle(775,875,150,25),
                new Rectangle(950,1000,150,25),
                new Rectangle(1250,1075,150,25),
                new Rectangle(1450,1100,1000,2000), //le village
                new Rectangle(1875,675,800,200),
                new Rectangle(2450,875,225,2000),
                new Rectangle(2675,775,100,2000),
                new Rectangle(2775,875,100,2000),
                new Rectangle(2875,975,100,2000),
                new Rectangle(2975,1475,600,1000),
                new Rectangle(3175,475,400,200),
                new Rectangle(3175,475,200,875),
                new Rectangle(3175,875,400,100),
                new Rectangle(450,-3000,200,2700),
                new Rectangle(900,-1000,25,500),
                new Rectangle(675,-1100,25,700),
                new Rectangle(1800,-1350,25,150),
                new Rectangle(650,-1350,1000,25),
                new Rectangle(800,-1800,1125,25),
                new Rectangle(1925,-1800,1000,250),
                new Rectangle(2925,-2800,1000,1250),
                new Rectangle(1650,-2800,1275,500),
                new Rectangle(2925,-1550,2275,1250),
                new Rectangle(650,-3000,1000,1025),
            };

            List<Rectangle> noWalljumpPlatforms = new List<Rectangle>
            {
                new Rectangle(0,-400,100, 800),
            };

            List<Rectangle> water = new List<Rectangle>
            {
                new Rectangle(2000,260,1200,215)
            };

            List<Rectangle> ice = new List<Rectangle>
            {
                new Rectangle(3575,1475,700,1000),
                new Rectangle(3575,500,2100,175),
                new Rectangle(4275,1575,150,2000),
                new Rectangle(4425,1675,150,2000),
                new Rectangle(4575,1775,500,2000),
                new Rectangle(5075,500,500,3000),
                new Rectangle(3575,875,100,100),
                new Rectangle(4375,1375,200,25),
                new Rectangle(4650,1275,200,25),
                new Rectangle(4300,1175,100,25),
                new Rectangle(4100,1075,50,25),
                new Rectangle(3800,975,50,25),
                new Rectangle(900,-600,25,100),
                new Rectangle(900,-800,25,100),
                new Rectangle(675,-700,25,100),
                new Rectangle(900,-900,25,100),
                new Rectangle(675,-1000,25,100),
                new Rectangle(900,-1100,25,100),
                new Rectangle(925,-1100,500,25),
                new Rectangle(1925,-1550,1000,1250),
                new Rectangle(3600,-300,1400,12)
            };

            List<Stalactite> stalactites = new List<Stalactite>
            {
            new Stalactite {
                // La stalactite elle-même
                Hitbox = new Rectangle(4100, 675, 50, 50),
                // La zone de détection
                DetectionZone = new Rectangle(4100, 725, 50, 300),
                SpawnY=675,
                Type = "glace"
                },
            new Stalactite {
                // La stalactite elle-même
                Hitbox = new Rectangle(3800, 675, 50, 50),
                // La zone de détection
                DetectionZone = new Rectangle(3800, 725, 50, 300),
                SpawnY=675,
                Type = "glace"
                },
            new Stalactite {
                // La stalactite elle-même
                Hitbox = new Rectangle(4325, 675, 50, 50),
                // La zone de détection
                DetectionZone = new Rectangle(4325, 725, 50, 475),
                SpawnY=675,
                Type = "glace"
                },
            new Stalactite {
                // La stalactite elle-même
                Hitbox = new Rectangle(4675, 1300, 50, 50),
                // La zone de détection
                DetectionZone = new Rectangle(4675, 1300, 50, 500),
                SpawnY=1300,
                Type = "glace"
                },
            new Stalactite {
                // La stalactite elle-même
                Hitbox = new Rectangle(650, -1975, 50, 50),
                // La zone de détection
                DetectionZone = new Rectangle(650, -1975, 50, 500),
                SpawnY=-1975,
                Type = "normal"
                }
            };
            //zones qui declenchent la reaparition (vide)
            List<Death> deathZone = new List<Death>
            {
                new Death{
                    Hitbox= new Rectangle(0,1500,1450,100),
                    isVoid=true
                    },
            };



            float stalactiteGravity = 1200f; // Gravité propre à la stalactite pour qu'elle tombe vite !



            float interactionRadius = 200f; // Distance max pour interagir avec le levier
            Rectangle slimeWBarrier = new Rectangle(3625, 675, 25, 200);
            bool slimeWLeverActivated = false; // Le levier commence "non activé"
            Rectangle slimeWLeverTrigger = new Rectangle(3675, 905, 40, 40);



            // Chargement du sprite
            string executionPath = AppDomain.CurrentDomain.BaseDirectory;
            string imagePath = Path.Combine(executionPath, "Sprites", "Hero-walk-Sheet.png");

            Texture2D knightSprite = Raylib.LoadTexture(imagePath);
            Raylib.SetTextureFilter(knightSprite, TextureFilter.Point);

            int totalFrames = 6;
            int currentFrame = 0;
            float frameTimer = 0.0f;
            float frameSpeed = 0.1f;

            int frameWidth = knightSprite.Width / totalFrames;
            int frameHeight = knightSprite.Height;
            float frameScale = 4.0f;

            // --- REGLAGES DE LA HITBOX ---
            // On définit la taille de la boîte de collision rouge (le corps)
            float hitboxWidth = 10 * frameScale;
            float hitboxHeight = 21 * frameScale;

            // Taille réelle que doit faire le sprite entier à l'écran
            float spriteRenderWidth = frameWidth * frameScale;
            float spriteRenderHeight = frameHeight * frameScale;

            // Décalage pour centrer le sprite par rapport à la hitbox
            // (À ajuster en pixels si le corps n'est pas tout à fait au milieu de la frame)
            float offsetX = (spriteRenderWidth - hitboxWidth) / 2;
            float offsetY = (spriteRenderHeight - hitboxHeight); // Aligne le bas du sprite et de la hitbox

            bool isMoving = false;
            float flipX = 1.0f;

            Rectangle sourceRec = new Rectangle(0, 0, frameWidth, frameHeight);

            // Le joueur physique (la hitbox)
            Rectangle player = new Rectangle(
                (int)playerpos.X,
                (int)playerpos.Y,
                (int)hitboxWidth,
                (int)hitboxHeight
            );
            playerpos.Y = -500;

            Camera2D camera = new Camera2D();
            camera.Offset = new Vector2(Raylib.GetScreenWidth() / 2, Raylib.GetScreenHeight() / 2);
            camera.Zoom = 1;

            while (!Raylib.WindowShouldClose())
            {

                float deltaTime = Raylib.GetFrameTime();

                if (Raylib.IsKeyPressed(KeyboardKey.P))
                {
                    isPlaying = !isPlaying;
                }

                if (wallJumpLockTimer > 0) wallJumpLockTimer -= deltaTime;
                if (isInWater == true) waterTime -= deltaTime;

                if (canDoubleJumpAndDash && (isGrounded || isTouchingWallLeft || isTouchingWallRight))
                {
                    doubleJumpCounter = 1;
                }

                // Rechargement quand on touche le sol ou un mur
                if (isGrounded || isTouchingWallLeft || isTouchingWallRight || isInWater)
                {
                    if (dashCooldownTimer <= 0)
                    {
                        canDash = true;
                    }
                }

                // Mise à jour du timer de cooldown
                if (dashCooldownTimer > 0)
                {
                    dashCooldownTimer -= deltaTime;
                }


                if (currentSpeedX != 0)
                {
                    flipX = currentSpeedX > 0 ? 1 : -1;
                }

                isGrounded = false;
                isMoving = false;
                isInWater = false;



                // --- 1. MISE À JOUR DE LA CAMERA ---
                camera.Offset = new Vector2(Raylib.GetScreenWidth() / 2.0f, Raylib.GetScreenHeight() / 2.0f);
                Vector2 targetPosition = new Vector2(player.X + player.Width / 2, player.Y + player.Height / 2);
                camera.Target = Vector2.Lerp(camera.Target, targetPosition, cameraSmoothing * deltaTime);

                // --- 2. DÉTECTION DU TYPE DE SOL (AU SOL UNIQUEMENT) ---
                bool isOnIce = false;
                bool isOnNormalPlatform = false;

                // On réutilise ton capteur de pieds actuel
                Rectangle feetSensor = new Rectangle(player.X, player.Y + player.Height, player.Width, 2);

                // 1. On regarde si on touche de la glace
                foreach (var gla in ice)
                {
                    if (Raylib.CheckCollisionRecs(feetSensor, gla))
                    {
                        isOnIce = true;
                        break;
                    }
                }
                // 2. On regarde si on touche une plateforme normale
                if (!isOnIce)
                {
                    foreach (var plat in platforms)
                    {
                        if (Raylib.CheckCollisionRecs(feetSensor, plat))
                        {
                            isOnNormalPlatform = true;
                            break;
                        }
                    }
                }
                foreach (var wat in water)
                {
                    if (Raylib.CheckCollisionRecs(player, wat))
                    {
                        isInWater = true;
                        break;
                    }
                }

                // 3. ON APPLIQUE LES RÈGLES UNIQUEMENT SI ON EST TOUCHÉ PAR UN SOL
                // Si on est en l'air, on ne rentre pas dans ces conditions, donc on GARDE l'inertie du dernier sol !
                if (isInWater)
                {
                    // L'eau prime sur le sol : on réduit la vitesse max même si on touche le fond
                    maxSpeedX = 150f;
                }
                else if (isOnIce)
                {
                    acceleration = 500f;
                    maxSpeedX = 400f;
                }
                else if (isOnNormalPlatform || isTouchingWallLeft || isTouchingWallRight)
                {
                    acceleration = 10000f;
                    maxSpeedX = 250f;
                }


                // --- 3. DÉPLACEMENT HORIZONTAL (ENTRÉES + FRICTION) ---
                // --- DÉCLENCHEMENT DU DASH ---
                if (Raylib.IsKeyPressed(KeyboardKey.LeftShift) && canDash && !isInDialogue && canDoubleJumpAndDash && isPlaying)
                {
                    isDashing = true;
                    canDash = false;
                    dashTimer = dashDuration;
                    dashCooldownTimer = dashCooldown;

                }

                // --- ÉTAT PENDANT LE DASH ---
                if (isDashing)
                {
                    dashTimer -= deltaTime;

                    // 1. Vitesse horizontale constante et élevée
                    currentSpeedX = flipX * dashSpeed;

                    // 2. Annule complètement la gravité pendant le dash
                    downspeed = 0;

                    // Fin du dash
                    if (dashTimer <= 0)
                    {
                        isDashing = false;
                        if (isOnIce)
                        {
                            currentSpeedX = 400f;
                        }
                        else if (isInWater)
                        {
                            currentSpeedX = 150f;
                        }
                        else
                        {
                            currentSpeedX = 250f;
                        }
                    }
                }


                bool inputPressed = false;
                if (!isInDialogue && wallJumpLockTimer <= 0f && isPlaying)
                {
                    if (Raylib.IsKeyDown(KeyboardKey.A))
                    {
                        currentSpeedX -= acceleration * deltaTime;
                        isMoving = true;
                        inputPressed = true;
                    }
                    if (Raylib.IsKeyDown(KeyboardKey.D))
                    {
                        currentSpeedX += acceleration * deltaTime;
                        isMoving = true;
                        inputPressed = true;
                    }
                }


                // Application de la friction selon le sol
                if (!inputPressed)
                {
                    // Si on est en l'air, on met une friction très faible (ex: 200f) pour planer,
                    // sinon on applique la friction du sol actuel
                    float friction = normalFriction;

                    if (!isGrounded)
                    {
                        friction = 200f; // Friction aérienne (garde l'élan en l'air)
                    }
                    else if (acceleration == 500f) // Une astuce pour savoir si on était configuré sur de la glace
                    {
                        friction = iceFriction;
                    }

                    if (currentSpeedX > 0)
                    {
                        currentSpeedX -= friction * deltaTime;
                        if (currentSpeedX < 0) currentSpeedX = 0;
                    }
                    else if (currentSpeedX < 0)
                    {
                        currentSpeedX += friction * deltaTime;
                        if (currentSpeedX > 0) currentSpeedX = 0;
                    }
                }

                // Limitation de la vitesse max
                if (!isDashing)
                {
                    if (currentSpeedX > maxSpeedX) currentSpeedX = maxSpeedX;
                    if (currentSpeedX < -maxSpeedX) currentSpeedX = -maxSpeedX;
                }
                // Forcer l'animation si le personnage glisse
                if (MathF.Abs(currentSpeedX) > 10f) isMoving = true;

                // --- 4. PHYSIQUE ET COLLISIONS X ---
                float moveX = currentSpeedX * deltaTime;

                if (isPlaying)
                {
                    playerpos.X += moveX;
                    player.X = playerpos.X;
                }

                if (!slimeWLeverActivated) // On ne teste la collision que si la barrière est fermée
                {
                    if (Raylib.CheckCollisionRecs(player, slimeWBarrier))
                    {
                        if (moveX > 0) playerpos.X = slimeWBarrier.X - player.Width;
                        else if (moveX < 0) playerpos.X = slimeWBarrier.X + slimeWBarrier.Width;
                        player.X = playerpos.X;
                        currentSpeedX = 0;
                    }
                }

                foreach (var plat in platforms)
                {
                    if (Raylib.CheckCollisionRecs(player, plat))
                    {
                        if (moveX > 0) playerpos.X = plat.X - player.Width;
                        else if (moveX < 0) playerpos.X = plat.X + plat.Width;
                        player.X = playerpos.X;
                        currentSpeedX = 0;
                        maxSpeedX = 250f;
                        if (currentSpeedX > maxSpeedX) currentSpeedX = maxSpeedX;
                    }
                }
                foreach (var plat in noWalljumpPlatforms)
                {
                    if (Raylib.CheckCollisionRecs(player, plat))
                    {
                        if (moveX > 0) playerpos.X = plat.X - player.Width;
                        else if (moveX < 0) playerpos.X = plat.X + plat.Width;
                        player.X = playerpos.X;
                        currentSpeedX = 0;
                        maxSpeedX = 250f;
                        if (currentSpeedX > maxSpeedX) currentSpeedX = maxSpeedX;
                    }
                }
                foreach (var gla in ice)
                {
                    if (Raylib.CheckCollisionRecs(player, gla))
                    {
                        if (moveX > 0) playerpos.X = gla.X - player.Width;
                        else if (moveX < 0) playerpos.X = gla.X + gla.Width;
                        player.X = playerpos.X;
                        currentSpeedX = 0;
                    }
                }



                // --- DÉTECTION DES MURS POUR LE WALL JUMP ---
                isTouchingWallLeft = false;
                isTouchingWallRight = false;

                // On crée deux rectangles fins sur les côtés gauche et droit de la hitbox du joueur
                Rectangle leftSensor = new Rectangle(player.X - 2, player.Y + 4, 2, player.Height - 8);
                Rectangle rightSensor = new Rectangle(player.X + player.Width, player.Y + 4, 2, player.Height - 8);

                //outils de debug
                if (Raylib.IsKeyPressed(KeyboardKey.Enter) && isPlaying)
                {
                    canWallJump = true;
                    canSwim = true;
                    canDoubleJumpAndDash = true;
                    devMode = true;
                }
                if (devMode)
                {
                    // Clic Droit : Téléportation en tenant compte de la caméra
                    if (Raylib.IsMouseButtonPressed(MouseButton.Right))
                    {
                        // Obtient la position de la souris à l'écran
                        Vector2 mouseScreenPos = Raylib.GetMousePosition();

                        // La convertit en coordonnées dans le monde du jeu grâce à la caméra
                        Vector2 mouseWorldPos = Raylib.GetScreenToWorld2D(mouseScreenPos, camera);

                        // Repositionne le joueur
                        playerpos.X = mouseWorldPos.X - (hitboxWidth / 2f);
                        playerpos.Y = mouseWorldPos.Y - (hitboxHeight / 2f);
                    }
                }

                // On vérifie si ces capteurs touchent une plateforme normale
                foreach (var plat in platforms)
                {
                    if (Raylib.CheckCollisionRecs(leftSensor, plat)) isTouchingWallLeft = true;
                    if (Raylib.CheckCollisionRecs(rightSensor, plat)) isTouchingWallRight = true;
                }

                // --- 5. GESTION DE L'ANIMATION SPRITE ---
                if (isMoving)
                {
                    frameTimer += deltaTime;
                    if (frameTimer >= frameSpeed)
                    {
                        frameTimer = 0.0f;
                        currentFrame++;
                        if (currentFrame >= totalFrames) currentFrame = 0;

                        sourceRec.X = currentFrame * frameWidth;
                    }
                }
                else
                {
                    currentFrame = 0;
                    sourceRec.X = 0;
                }

                sourceRec.Width = frameWidth * flipX;

                // --- 6. PHYSIQUE ET COLLISIONS Y ---
                if (isPlaying)
                {
                    downspeed += gravity * deltaTime;
                }
                // --- GLISSEMENT SUR LE MUR (WALL SLIDE) ---
                // On vérifie si le joueur touche un mur, qu'il est en l'air (!isGrounded), qu'il descend (downspeed > 0) et qu'il puisse wallJump
                if ((isTouchingWallLeft || isTouchingWallRight) && !isGrounded && downspeed > 0 && canWallJump)
                {
                    if (downspeed > maxWallSlideSpeed)
                    {
                        downspeed = maxWallSlideSpeed;
                    }
                }
                if (Raylib.IsKeyPressed(KeyboardKey.K))
                {
                    isPlaying = true;
                }

                // La physique de l'eau
                // --- PHYSIQUE DE L'EAU ET NAGE ---
                if (isInWater)
                {
                    if (canSwim)
                    {
                        // 1. On réinitialise le timer de noyade (le joueur ne meurt plus)
                        waterTime = 5.0f;

                        // 2. Trouver la surface de l'eau qu'on touche
                        Rectangle currentWater = water.FirstOrDefault(w => Raylib.CheckCollisionRecs(player, w));

                        if (currentWater.Width > 0)
                        {
                            // Position CIBLE pour que le joueur soit "à moitié submergé"
                            // Ex: Le haut du joueur dépasse de la moitié de sa hauteur par rapport à la surface
                            float waterSurfaceY = currentWater.Y - (player.Height / 2f);

                            // Si le joueur est en train de tomber dans l'eau
                            if (downspeed > 0)
                            {
                                // Amortissement de la chute dans l'eau (effet d'impact fluide)
                                downspeed = Vector2.Lerp(new Vector2(0, downspeed), Vector2.Zero, 10f * deltaTime).Y;

                                // Si le joueur est descendu plus bas que la surface
                                if (playerpos.Y > waterSurfaceY)
                                {
                                    // On le fait flotter doucement vers la surface (poussée d'Archimède)
                                    playerpos.Y = MathF.Min(playerpos.Y, waterSurfaceY);
                                    downspeed = 0;
                                }
                            }
                            else if (downspeed == 0 && playerpos.Y > waterSurfaceY)
                            {
                                // Flottabilité passive : ramène le personnage à la surface s'il s'enfonce
                                playerpos.Y = Vector2.Lerp(new Vector2(0, playerpos.Y), new Vector2(0, waterSurfaceY), 8f * deltaTime).Y;
                            }
                        }

                        // 3. Vitesse de déplacement horizontal à la surface
                        maxSpeedX = 200f;

                        // 4. SAUT DEPUIS LA SURFACE
                        // Permet de sauter hors de l'eau avec la touche Espace
                        if (Raylib.IsKeyPressed(KeyboardKey.Space) && isPlaying)
                        {
                            downspeed = -450f; // Impulsion de saut pour sortir du lac
                        }
                    }
                    else
                    {
                        // SANS POUVOIR : Le joueur coule et se noie
                        if (downspeed > 100f) downspeed = 100f;
                    }
                }

                float moveY = (float)downspeed * deltaTime;
                if (isPlaying)
                {
                    playerpos.Y += moveY;
                    player.Y = playerpos.Y;
                }
                if (!slimeWLeverActivated)
                {
                    if (Raylib.CheckCollisionRecs(player, slimeWBarrier))
                    {
                        if (downspeed > 0)
                        {
                            playerpos.Y = slimeWBarrier.Y - player.Height;
                            downspeed = 0;
                            isGrounded = true;
                        }
                        else if (downspeed < 0)
                        {
                            playerpos.Y = slimeWBarrier.Y + slimeWBarrier.Height;
                            downspeed = 0;
                        }
                        player.Y = playerpos.Y;
                    }
                }

                foreach (var plat in platforms)
                {
                    if (Raylib.CheckCollisionRecs(player, plat))
                    {
                        if (downspeed > 0)
                        {
                            playerpos.Y = plat.Y - player.Height;
                            downspeed = 0;
                            isGrounded = true;
                        }
                        else if (downspeed < 0)
                        {
                            playerpos.Y = plat.Y + plat.Height;
                            downspeed = 0;
                        }
                        player.Y = playerpos.Y;
                    }
                }

                foreach (var plat in noWalljumpPlatforms)
                {
                    if (Raylib.CheckCollisionRecs(player, plat))
                    {
                        if (downspeed > 0)
                        {
                            playerpos.Y = plat.Y - player.Height;
                            downspeed = 0;
                            isGrounded = true;
                        }
                        else if (downspeed < 0)
                        {
                            playerpos.Y = plat.Y + plat.Height;
                            downspeed = 0;
                        }
                        player.Y = playerpos.Y;
                    }
                }

                foreach (var gla in ice)
                {
                    if (Raylib.CheckCollisionRecs(player, gla))
                    {
                        if (downspeed > 0)
                        {
                            playerpos.Y = gla.Y - player.Height;
                            downspeed = 0;
                            isGrounded = true;
                        }
                        else if (downspeed < 0)
                        {
                            playerpos.Y = gla.Y + gla.Height;
                            downspeed = 0;
                        }
                        player.Y = playerpos.Y;
                    }
                }

                // --- 7. SENSORS SAUT / COYOTE TIME ---
                if (Raylib.IsKeyPressed(KeyboardKey.Space) && isPlaying) jumpBufferCounter = jumpBufferDuration;
                else jumpBufferCounter -= deltaTime;

                if (isGrounded && !isInWater)
                {
                    coyoteTimeCounter = coyoteTimeDuration;
                    lastSafePosition = playerpos;
                }
                else coyoteTimeCounter -= deltaTime;

                if (jumpBufferCounter > 0 && !isInWater && !isInDialogue)
                {
                    // 1. Saut Normal au sol
                    if (coyoteTimeCounter > 0)
                    {
                        downspeed = -500;
                        coyoteTimeCounter = 0;
                        jumpBufferCounter = 0;
                    }
                    // 2. Wall Jump sur un mur à GAUCHE (on est propulsé vers la DROITE)
                    else if (canWallJump && isTouchingWallLeft && !isGrounded)
                    {
                        downspeed = wallJumpForceY;
                        currentSpeedX = wallJumpForceX; // Impulsion vers la droite
                        jumpBufferCounter = 0;
                        coyoteTimeCounter = 0;
                        wallJumpLockTimer = 0.4f;
                    }
                    // 3. Wall Jump sur un mur à DROITE (on est propulsé vers la GAUCHE)
                    else if (canWallJump && isTouchingWallRight && !isGrounded)
                    {
                        downspeed = wallJumpForceY;
                        currentSpeedX = -wallJumpForceX; // Impulsion vers la gauche
                        jumpBufferCounter = 0;
                        coyoteTimeCounter = 0;
                        wallJumpLockTimer = 0.4f;
                    }
                    else if (canDoubleJumpAndDash && doubleJumpCounter != 0 && !isGrounded)
                    {
                        downspeed = -450;
                        doubleJumpCounter = 0;
                        jumpBufferCounter = 0;
                    }
                }

                if (Raylib.IsKeyReleased(KeyboardKey.Space) && downspeed < 0)
                {
                    downspeed *= 0.5f;
                }

                foreach (var stal in stalactites)
                {
                    // --- LOGIQUE DE REAPPARITION DES STALACTITES---
                    if (stal.IsDestroyed)
                    {
                        stal.RespawnTimer += deltaTime; // On compte les secondes

                        if (stal.RespawnTimer >= stal.RespawnDelay)
                        {
                            // Reset complet de la stalactite !
                            stal.IsDestroyed = false;
                            stal.IsFalling = false;
                            stal.FallSpeed = 0f;
                            stal.Hitbox.Y = stal.SpawnY; // Elle revient au plafond
                            stal.RespawnTimer = 0f;      // On remet le chrono à zéro
                        }

                        continue; // Tant qu'elle n'a pas fini de recharger, on ignore le reste de sa physique
                    }
                    // 1. Détection du joueur
                    if (!stal.IsFalling && Raylib.CheckCollisionRecs(player, stal.DetectionZone))
                    {
                        stal.IsFalling = true;
                    }

                    // 2. Si elle tombe, on applique la physique
                    if (stal.IsFalling)
                    {
                        stal.FallSpeed += stalactiteGravity * deltaTime;
                        stal.Hitbox.Y += stal.FallSpeed * deltaTime;

                        // Collision avec le JOUEUR
                        if (Raylib.CheckCollisionRecs(player, stal.Hitbox))
                        {
                            stal.IsDestroyed = true;
                            wallJumpLockTimer = 0.8f;  //Permet d'éviter que le joueur se raccroche au mur a la frame après s'être fait toucher 

                            // --- SYSTÈME DE KNOCKBACK CORRIGÉ ---
                            float pushDirection = 0f;

                            if (isTouchingWallRight)
                            {
                                // Si on est sur le mur de droite, on est éjecté vers la GAUCHE
                                pushDirection = -1.0f;
                            }
                            else if (isTouchingWallLeft)
                            {
                                // Si on est sur le mur de gauche, on est éjecté vers la DROITE
                                pushDirection = 1.0f;
                            }
                            else
                            {
                                // Comportement classique si on est en plein vol (basé sur le centre)
                                pushDirection = (player.X + player.Width / 2 > stal.Hitbox.X + stal.Hitbox.Width / 2) ? 1.0f : -1.0f;
                            }

                            currentSpeedX = pushDirection * 500f; // Propulse le joueur horizontalement
                            downspeed = -200f;                    // Un petit saut vers le haut pour le décoller du sol
                            coyoteTimeCounter = 0;                // Annule le coyote time pour l'empêcher de sauter immédiatement

                        }

                        // Collision avec le SOL (Plateformes ou Glace)
                        foreach (var plat in platforms)
                        {
                            if (Raylib.CheckCollisionRecs(stal.Hitbox, plat)) stal.IsDestroyed = true;
                        }
                        foreach (var gla in ice)
                        {
                            if (Raylib.CheckCollisionRecs(stal.Hitbox, gla)) stal.IsDestroyed = true;
                        }
                    }
                }


                // --- INTERACTION LEVIER ---
                Vector2 playerCenter = new Vector2(player.X + player.Width / 2, player.Y + player.Height / 2);
                Vector2 leverCenter = new Vector2(slimeWLeverTrigger.X + slimeWLeverTrigger.Width / 2, slimeWLeverTrigger.Y + slimeWLeverTrigger.Height / 2);

                if (Vector2.Distance(playerCenter, leverCenter) < interactionRadius && !slimeWLeverActivated)
                {
                    // Si on appuie sur E et qu'on ne parle pas encore : on lance le dialogue
                    if (Raylib.IsKeyPressed(KeyboardKey.E) && !isInDialogue && isPlaying)
                    {
                        isInDialogue = true;
                        dialogueStep = -1;
                        currentSpeedX = 0; // On stoppe net le joueur
                        slimeWLeverActivated = true;    // La barrière s'ouvre !
                        currentTalkingSlime = slimeW;
                    }
                }

                Vector2 slimeACenter = new Vector2(slimeA.body.X + slimeA.body.Width / 2, slimeA.body.Y + slimeA.body.Height / 2);

                if (Vector2.Distance(playerCenter, slimeACenter) < 200 && !slimeA.isAllied && playerpos.Y <= -1800)
                {
                    // Si on appuie sur E et qu'on ne parle pas encore : on lance le dialogue
                    if (!isInDialogue)
                    {
                        isInDialogue = true;
                        dialogueStep = 0;
                        currentSpeedX = 0; // On stoppe net le joueur
                        currentTalkingSlime = slimeA;
                    }
                }


                // --- GESTION DE LA PROGRESSION DU DIALOGUE ---
                if (isInDialogue && currentTalkingSlime != null)
                {
                    if (Raylib.IsKeyPressed(KeyboardKey.E) || Raylib.IsKeyPressed(KeyboardKey.Space) && isPlaying)
                    {
                        dialogueStep++;

                        // Si on a lu toutes les lignes
                        if (dialogueStep >= currentTalkingSlime.dialogue.Count)
                        {
                            isInDialogue = false;          // On ferme la boîte de dialogue

                            if (currentTalkingSlime == slimeW)
                            {
                                canWallJump = true;
                            }
                            else if (currentTalkingSlime == slimeA)
                            {
                                canSwim = true;
                            }
                            // Passer le bon slime en allié
                            currentTalkingSlime.isAllied = true;
                            // Réinitialiser la référence
                            currentTalkingSlime = null;
                        }
                    }
                }

                foreach (var death in deathZone)
                {
                    if (Raylib.CheckCollisionRecs(player, death.Hitbox))
                    {
                        // Téléportation au dernier sol stable
                        playerpos = lastSafePosition;
                        player.X = playerpos.X;
                        player.Y = playerpos.Y;

                        // On coupe net les vitesses pour éviter qu'il garde son élan de chute
                        downspeed = 0;
                        currentSpeedX = 0;
                        coyoteTimeCounter = 0;
                    }
                }

                if (waterTime <= 0)
                {
                    // Téléportation au dernier sol stable
                    playerpos = lastSafePosition;
                    player.X = playerpos.X;
                    player.Y = playerpos.Y;

                    // On coupe net les vitesses pour éviter qu'il garde son élan de chute
                    downspeed = 0;
                    currentSpeedX = 0;
                    coyoteTimeCounter = 0;


                    //reset du temps
                    waterTime = 5;
                }




                // --- 8. RENDU / DESSIN ---
                Raylib.BeginTextureMode(gameTarget);
                Raylib.ClearBackground(Color.SkyBlue);
                Raylib.BeginMode2D(camera);

                foreach (var plat in platforms)
                {
                    Raylib.DrawRectangleRec(plat, new Color(46, 63, 89, 255));
                }
                foreach (var plat in noWalljumpPlatforms)
                {
                    Raylib.DrawRectangleRec(plat, new Color(22, 31, 43, 255));
                }

                foreach (var gla in ice)
                {
                    Raylib.DrawRectangleRec(gla, new Color(73, 232, 255, 255));
                }

                float currentOffsetX = offsetX;
                if (flipX < 0)
                {
                    currentOffsetX = spriteRenderWidth - hitboxWidth - offsetX;
                }

                Rectangle destRec = new Rectangle(
                    player.X - currentOffsetX,
                    player.Y - offsetY,
                    spriteRenderWidth,
                    spriteRenderHeight
                );

                Vector2 origin = new Vector2(0, 0);

                Raylib.DrawTexturePro(
                    knightSprite,
                    sourceRec,
                    destRec,
                    origin,
                    0.0f,
                    Color.White
                );

                foreach (var wat in water)
                {
                    Raylib.DrawRectangleRec(wat, Raylib.Fade(Color.Blue, 0.4f));
                }

                foreach (var stal in stalactites)
                {
                    if (stal.IsDestroyed) continue;

                    // AFFICHER LA ZONE DE DÉTECTION
                    Raylib.DrawRectangleRec(stal.DetectionZone, Raylib.Fade(Color.Orange, 0.1f));

                    // DESSINER LA STALACTITE
                    if (stal.Type == "glace")
                    {
                        Raylib.DrawRectangleRec(stal.Hitbox, new Color(200, 245, 255, 255));
                    }
                    else if (stal.Type == "normal")
                    {
                        Raylib.DrawRectangleRec(stal.Hitbox, new Color(61, 84, 117, 255));
                    }
                }

                // Dessiner le levier (Rouge = Fermé, Vert = Ouvert)
                Color leverColor = slimeWLeverActivated ? Color.Green : Color.Red;
                Raylib.DrawRectangleRec(slimeWLeverTrigger, leverColor);

                // Afficher un petit texte d'indication si le joueur est proche
                if (Vector2.Distance(playerCenter, leverCenter) < interactionRadius && !slimeWLeverActivated)
                {
                    Raylib.DrawText("Appuyez sur E", (int)slimeWLeverTrigger.X - 20, (int)slimeWLeverTrigger.Y - 20, 10, Color.White);
                }

                // Dessiner la barrière seulement si elle est fermée
                if (!slimeWLeverActivated)
                {
                    Raylib.DrawRectangleRec(slimeWBarrier, Raylib.Fade(Color.Gray, 0.8f));
                }

                foreach (var slime in slimes)
                {
                    // On ne le dessine que s'il n'est pas encore allié
                    if (!slime.isAllied)
                    {
                        Raylib.DrawRectangleRec(slime.body, slime.Color);
                    }
                }

                Raylib.DrawRectangleRec(player, Raylib.Fade(Color.Red, 0.4f));
                foreach (var death in deathZone)
                {
                    if (death.isVoid)
                    {
                        Raylib.DrawRectangleRec(death.Hitbox, Raylib.Fade(Color.Black, 0.4f));
                    }
                    else
                    {
                        Raylib.DrawRectangleRec(death.Hitbox, new Color(240, 240, 240, 255));
                    }
                }
                if (!devMode)
                {
                    // --- DESSINER L'OMBRE AUTOUR DU JOUEUR ---

                    // Rectangle source de la texture (Inversé sur Y comme toujours dans Raylib)
                    Rectangle maskSrcRec = new Rectangle(0, 0, lightMask.Texture.Width, -lightMask.Texture.Height);

                    // Rectangle de destination centré sur le joueur
                    Rectangle maskDestRec = new Rectangle(
                        playerpos.X + player.Width / 2 - lightSize / 2f,
                        playerpos.Y + player.Height / 2 - lightSize / 2f,
                        lightSize,
                        lightSize
                    );

                    // Dessin de la lumière
                    Raylib.DrawTexturePro(lightMask.Texture, maskSrcRec, maskDestRec, Vector2.Zero, 0f, Color.White);

                    // 2. Remplir le reste de la carte autour du carré de lumière
                    Raylib.DrawRing(new Vector2(playerpos.X + player.Width / 2, playerpos.Y + player.Height / 2), lightSize / 2 - 2, 5000f, 0, 360, 60, new Color(0, 0, 0, 255));

                }
                Raylib.EndMode2D();

                // --- RENDU DU DIALOGUE (FIXE À L'ÉCRAN) ---
                if (isInDialogue)
                {

                    // Dimensions de la boîte de dialogue
                    int boxWidth = screenWidth - 100;
                    int boxHeight = 150;
                    int boxX = 50;
                    int boxY = screenHeight - boxHeight - 50;

                    // Fond noir semi-transparent et bordure blanche
                    Raylib.DrawRectangle(boxX, boxY, boxWidth, boxHeight, Raylib.Fade(Color.Black, 0.85f));
                    Raylib.DrawRectangleLines(boxX, boxY, boxWidth, boxHeight, Color.White);

                    // Affichage du texte de l'étape actuelle
                    Raylib.DrawText(currentTalkingSlime.dialogue[dialogueStep], boxX + 30, boxY + 40, 24, Color.White);

                    // Petit indicateur pour passer au texte suivant
                    Raylib.DrawText("[E] ou [Espace] pour continuer", boxX + boxWidth - 300, boxY + boxHeight - 30, 16, Color.Gray);
                }

                Raylib.EndTextureMode();
                Raylib.BeginDrawing();
                Raylib.ClearBackground(Color.Black);

                // Rectangle source de la RenderTarget (Attention : Y est inversé dans Raylib pour les RenderTexture!)
                Rectangle screenSrcRec = new Rectangle(0, 0, gameTarget.Texture.Width, -gameTarget.Texture.Height);
                Rectangle screenDestRec = new Rectangle(0, 0, screenWidth, screenHeight);

                // Si le jeu est en pause, on active le shader de flou
                if (!isPlaying)
                {
                    Raylib.BeginShaderMode(blurShader);
                }

                // On dessine l'image du jeu capturée sur tout l'écran
                Raylib.DrawTexturePro(gameTarget.Texture, screenSrcRec, screenDestRec, Vector2.Zero, 0.0f, Color.White);

                if (!isPlaying)
                {
                    Raylib.EndShaderMode();

                    // Assombrir légèrement l'écran par-dessus le flou
                    Raylib.DrawRectangle(0, 0, screenWidth, screenHeight, Raylib.Fade(Color.Black, 0.3f));

                    // DESSINER LE MENU PAUSE / START PAR-DESSUS LE FLOU
                    string pauseText = "PAUSE";
                    int fontSize = 60;
                    int textWidth = Raylib.MeasureText(pauseText, fontSize);
                    Raylib.DrawText(pauseText, screenWidth / 2 - textWidth / 2, screenHeight / 2 - 30, fontSize, Color.White);
                    Raylib.DrawText("Appuyez sur [P] pour reprendre", screenWidth / 2 - 150, screenHeight / 2 + 50, 20, Color.LightGray);
                }

                // --- UI HORS PAUSE (ex: Boîte de dialogue) ---
                if (isInDialogue)
                {
                    int boxWidth = screenWidth - 100;
                    int boxHeight = 150;
                    int boxX = 50;
                    int boxY = screenHeight - boxHeight - 50;

                    Raylib.DrawRectangle(boxX, boxY, boxWidth, boxHeight, Raylib.Fade(Color.Black, 0.85f));
                    Raylib.DrawRectangleLines(boxX, boxY, boxWidth, boxHeight, Color.White);
                    Raylib.DrawText(currentTalkingSlime.dialogue[dialogueStep], boxX + 30, boxY + 40, 24, Color.White);
                }

                Raylib.EndDrawing();
            }

            Raylib.CloseWindow();
            Raylib.UnloadTexture(knightSprite);
            Raylib.UnloadRenderTexture(lightMask);
            Raylib.UnloadRenderTexture(gameTarget);
            Raylib.UnloadShader(blurShader);
        }
    }
}