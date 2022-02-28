﻿using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace MoreStaves.Projectiles
{
	// Adds the Chlorophyte Minion as a projectile.
	public class ChlorophyteMinion : ModProjectile
	{
		public override void SetStaticDefaults()
		{
			DisplayName.SetDefault("Chlorophyte");

			// Sets the amount of frames this minion has on its spritesheet
			Main.projFrames[projectile.type] = 4;
			// This is necessary for right-click targeting
			ProjectileID.Sets.MinionTargettingFeature[projectile.type] = true;

			// Denotes that this projectile is a pet or minion
			Main.projPet[projectile.type] = true;
			// Ensures minion can properly spawn when summoned and is replaced when other minions are summoned
			ProjectileID.Sets.MinionSacrificable[projectile.type] = true;
			// Damage reduction related to homing attacks
			ProjectileID.Sets.Homing[projectile.type] = true;
		}

		public sealed override void SetDefaults()
		{
			projectile.width = 46;
			projectile.height = 46;

			// Makes the minion go through tiles freely
			projectile.tileCollide = false;
			projectile.friendly = true;

			// Deals minion damage
			projectile.minion = true;

			// Number of minion slots used
			projectile.minionSlots = 1f;

			// Prevents being destroyed on collision
			projectile.penetrate = -1;
		}

		// Prevents tiles being broken by minion
		public override bool? CanCutTiles()
		{
			return false;
		}

		// Disallows minion to deal contact damage
		public override bool MinionContactDamage()
		{
			return false;
		}

		// Attack delay
		int delay = 0;
		public override void AI()
		{
			Player player = Main.player[projectile.owner];

			#region Active check
			// This is the "active check", makes sure the minion is alive while the player is alive, and despawns if not
			if (player.dead || !player.active)
			{
				player.ClearBuff(ModContent.BuffType<Buffs.ChlorophyteBuff>());
			}
			if (player.HasBuff(ModContent.BuffType<Buffs.ChlorophyteBuff>()))
			{
				projectile.timeLeft = 2;
			}
			#endregion

			#region General behavior
			Vector2 idlePosition = player.Center;
			idlePosition.Y -= 48f;

			// Finds idle position related to other spawned minions
			float minionPositionOffsetX = (10 + projectile.minionPos * 40) * -player.direction;
			idlePosition.X += minionPositionOffsetX;

			// All of this code below this line is adapted from Spazmamini code (ID 388, aiStyle 66)

			// Teleport to player if distance is too big
			Vector2 vectorToIdlePosition = idlePosition - projectile.Center;
			float distanceToIdlePosition = vectorToIdlePosition.Length();
			if (Main.myPlayer == player.whoAmI && distanceToIdlePosition > 2000f)
			{
				// Whenever you deal with non-regular events that change the behavior or position drastically, make sure to only run the code on the owner of the projectile,
				// and then set netUpdate to true
				projectile.position = idlePosition;
				projectile.velocity *= 0.1f;
				projectile.netUpdate = true;
			}

			// If your minion is flying, you want to do this independently of any conditions
			float overlapVelocity = 0.04f;
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				// Fix overlap with other minions
				Projectile other = Main.projectile[i];
				if (i != projectile.whoAmI && other.active && other.owner == projectile.owner && Math.Abs(projectile.position.X - other.position.X) + Math.Abs(projectile.position.Y - other.position.Y) < projectile.width)
				{
					if (projectile.position.X < other.position.X) projectile.velocity.X -= overlapVelocity;
					else projectile.velocity.X += overlapVelocity;

					if (projectile.position.Y < other.position.Y) projectile.velocity.Y -= overlapVelocity;
					else projectile.velocity.Y += overlapVelocity;
				}
			}
			#endregion

			#region Find target
			// Starting search distance
			float distanceFromTarget = 700f;
			Vector2 targetCenter = projectile.position;
			Vector2 targetBehind = projectile.position;
			bool foundTarget = false;

			// If the player has targetted an npc then check its validity as a target
			if (player.HasMinionAttackTargetNPC)
			{
				NPC npc = Main.npc[player.MinionAttackTargetNPC];
				float between = Vector2.Distance(npc.Center, projectile.Center);
				if (between < 2000f)
				{
					distanceFromTarget = between;
					targetCenter = npc.Center;
					targetBehind = npc.Center - new Vector2(npc.direction * 50, 30);
					foundTarget = true;
				}
			}
			// If no target is currently found then search all NPCs and find the closest one
			if (!foundTarget)
			{
				// This code is required either way, used for finding a target
				for (int i = 0; i < Main.maxNPCs; i++)
				{
					NPC npc = Main.npc[i];
					if (npc.CanBeChasedBy())
					{
						float between = Vector2.Distance(npc.Center, projectile.Center);
						bool closest = Vector2.Distance(projectile.Center, targetCenter) > between;
						bool inRange = between < distanceFromTarget;
						bool lineOfSight = Collision.CanHitLine(projectile.position, projectile.width, projectile.height, npc.position, npc.width, npc.height);
						bool closeThroughWall = between < 100f;
						if (((closest && inRange) || !foundTarget) && (lineOfSight || closeThroughWall))
						{
							distanceFromTarget = between;
							targetCenter = npc.Center;
							targetBehind = npc.Center - new Vector2(npc.direction * 50, 30);
							foundTarget = true;
						}
					}
				}
			}

			// Minion is friendly if it has no target
			projectile.friendly = foundTarget;
			#endregion

			#region Attack
			// Speed of projectile
			float projSpeed2 = 6f;
			if (delay == 0)
			{
				if (foundTarget)
				{
					delay = 120;
					Vector2 minionToProjectile = projectile.Center - targetCenter;
					minionToProjectile.Normalize();
					minionToProjectile *= projSpeed2;
					Vector2 velocity = -minionToProjectile;
					// Spawns a projectile in direction of target
					Projectile.NewProjectile(projectile.Center, velocity, ModContent.ProjectileType<ChlorophyteProjectile>(), 60, 8, projectile.owner);
				}
			}
			else
			{
				delay--;
			}
			#endregion

			#region Movement
			float speed = 8f;
			float inertia = 20f;

			if (foundTarget)
			{
				// The immediate range around the target (so it doesn't latch onto it when close)
				Vector2 direction = targetBehind - projectile.Center;
				direction.Normalize();
				direction *= speed;
				projectile.velocity = (projectile.velocity * (inertia - 1) + direction) / inertia;
			}
			else
			{
				// Minion doesn't have a target: return to player and idle
				if (distanceToIdlePosition > 600f)
				{
					// Speed up the minion if it's away from the player
					speed = 12f;
					inertia = 60f;
				}
				else
				{
					// Slow down the minion if closer to the player
					speed = 4f;
					inertia = 80f;
				}
				if (distanceToIdlePosition > 20f)
				{
					// The immediate range around the player (when it passively floats about)

					// This is a simple movement formula using the two parameters and its desired direction to create a "homing" movement
					vectorToIdlePosition.Normalize();
					vectorToIdlePosition *= speed;
					projectile.velocity = (projectile.velocity * (inertia - 1) + vectorToIdlePosition) / inertia;
				}
				else if (projectile.velocity == Vector2.Zero)
				{
					// If there is a case where it's not moving at all, give it a little "poke"
					projectile.velocity.X = -0.15f;
					projectile.velocity.Y = -0.05f;
				}
			}
			#endregion

			#region Animation and visuals
			// So it will lean slightly towards the direction it's moving
			if (foundTarget)
			{
				projectile.rotation = projectile.DirectionTo(targetCenter).ToRotation() - 180;
			}
			else
			{
				projectile.rotation = projectile.velocity.ToRotation() - 180;
			}


			int frameSpeed = 20;
			projectile.frameCounter++;
			if (projectile.frameCounter >= frameSpeed)
			{
				projectile.frameCounter = 0;
				projectile.frame++;
				if (projectile.frame >= Main.projFrames[projectile.type])
				{
					projectile.frame = 0;
				}
			}

			// Adds light around the minion
			Lighting.AddLight(projectile.Center, Color.White.ToVector3() * 0.78f);
			#endregion

		}
    }
}
