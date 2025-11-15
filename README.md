# AutoPOE - Advanced Simulacrum Farming Bot

An intelligent, strategy-based farming bot for Path of Exile's Simulacrum encounters using the Exile API framework. Features sophisticated combat strategies with boss lock mechanics, intelligent skill management, and automated incubator application.

## Key Features

- **Strategic Combat System**: Multiple combat strategies with boss priority targeting
- **Boss Lock System**: Automatically locks onto high-priority bosses (Kosis, Omniphobia) with dynamic repositioning
- **Intelligent Skill Management**: Role-based skill system with priority-based casting
- **Automated Incubator Application**: Applies incubators from stash to equipment automatically
- **Smart Action Transitions**: Stays in combat mode until all monsters are cleared
- **Debug Visualization**: Comprehensive overlay showing bot status, targeting, and combat info
- **Equipment Calibration**: Visual calibration system for precise equipment slot positioning

## Combat Strategies

### Standard Strategy

- **Best For**: Balanced builds, general farming
- **Behavior**: Prioritizes rare and unique monsters, then closest enemies
- **Target Selection**: Focuses on highest value targets for efficient clearing
- **Boss Handling**: No boss lock - switches targets based on proximity

### Aggressive Strategy

- **Best For**: High DPS builds, Cast When Stunned (CWS) builds
- **Behavior**: Targets groups of enemies with target stability
- **Target Selection**: Finds center of largest enemy groups, maintains stable targeting
- **Boss Handling**:
  - Automatically detects and locks onto Simulacrum bosses (Kosis priority 2, Omniphobia/DeliriumBoss priority 1)
  - Scans extended range (2x normal combat range) for higher-priority bosses
  - Maintains lock on specific boss by name to prevent switching to lower-priority bosses
  - Disables normal repositioning when boss locked - stands still within 30 units, follows if boss moves farther
  - 30-second timeout and 150-unit max distance safety limits

## Skill Configuration

### Skill Roles

- **PrimaryDamage**: Your main attack skill
- **AreaDamage**: AOE skills for clearing groups
- **SingleTarget**: High damage skills for rares/bosses (used automatically when boss locked)
- **Defensive**: Guard skills and panic buttons
- **Buff**: Auras, buffs, and toggle skills
- **Movement**: Navigation and traversal skills

### Target Priorities

- **HighestThreat**: Rare/Unique monsters first, then closest
- **ClosestEnemy**: Always targets nearest enemy
- **RareFirst**: Only targets rare monsters
- **UniqueFirst**: Only targets unique monsters
- **MostEnemies**: Targets center of largest group
- **SelfTarget**: For self-cast skills (buffs, etc)
- **AllyTarget**: For mercenary/minion buffs

### Cast Types

- **TargetMonster**: Click on monsters
- **TargetSelf**: Cast on player position
- **TargetMercenary**: Cast on mercenary/allies
- **TargetGround**: Ground targeted skills
- **DoNotUse**: Disable the skill

## Setup Instructions

### Basic Configuration

1. Install the plugin in your ExileAPI
2. Configure at least one Movement skill (required for navigation)
3. Set up your combat skills with appropriate roles and priorities
4. Important: Include at least one blink skill (Frostblink/Flame Dash) for terrain navigation

### Skill Setup Examples

**Typical Melee Build:**

- Skill1 (Q): Main Attack - PrimaryDamage, HighestThreat, Priority 8
- Skill2 (W): Movement - Movement, SelfTarget, Priority 5
- Skill3 (E): Guard Skill - Defensive, SelfTarget, Priority 10
- Skill4 (R): Aura - Buff, SelfTarget, Priority 6

**Cast When Stunned Build:**

- Skill1 (Q): PenanceMark - SingleTarget, TargetMonster, Priority 8, 100ms delay
- Skill2 (W): Movement - Movement, SelfTarget, Priority 5
- Skill3 (E): Guard Skill - Defensive, SelfTarget, Priority 10
- Use Aggressive strategy for boss lock mechanics

### Combat Settings

- **Combat Strategy**: Choose Standard (balanced) or Aggressive (boss lock, CWS builds)
- **Defensive Threshold**: Health % to trigger defensive skills (default 50%)
- **Buff Maintenance**: Automatically maintain important buffs (recommended: ON)
- **Combat Distance**: Range for target detection (default 100)

### Incubator Setup

- Store incubators in your stash dump tab
- Enable "Use Incubators" setting
- Bot will automatically apply them to equipment during stash visits

## How To Use

1. **Preparation**:

   - Set your dump stash tab as active
   - Place Simulacrums in Map Device storage
   - Start in your hideout

2. **Start Farming**:

   - Press the bot start key (default: Insert)
   - Bot will handle everything automatically

3. **Monitoring**:
   - Enable Debug Mode to see bot status
   - Use Combat Debug toggle to see targeting info
   - Monitor the overlay for any issues

## Debug Features

### Debug Overlay Information

- **Bot Status**: Running state, focus detection, action timing
- **Location Info**: Current area, hideout detection
- **Combat Info**: Active strategy, target selection, skill usage, boss lock status
- **Monster Count**: Shows X/Y in range, stays in combat until 0 monsters remaining
- **Incubator Status**: Detection and application status

### Visual Debug Options

- **Draw Inventory**: Shows item boxes and equipment slots
- **Combat Debug**: Displays targeting and strategy information
- **Stash Debug**: Shows stash contents and incubator detection
- **Equipment Calibration**: Visual positioning system for equipment slots

## Important Notes

### Skill Configuration Tips

- **Movement Skills**: Set these as "Movement" role, never "DoNotUse"
- **Buff Skills**: Use the "Buff Name" field to prevent unnecessary recasting
- **Priority System**: Higher numbers = higher priority (1-10 scale)
- **Boss Lock Builds**: Use Aggressive strategy for CWS builds that need to stand still

### Common Issues

- **Bot gets stuck**: Ensure you have blink skills configured as movement
- **Skills not casting**: Check skill names match exactly (case sensitive)
- **Poor targeting**: Adjust Target Priority settings for your build
- **Boss switching**: Aggressive strategy prioritizes higher-priority bosses automatically
- **Equipment issues**: Use calibration mode to set precise positions

### Performance Tips

- Use Aggressive strategy for builds that benefit from boss lock (CWS, high single-target DPS)
- Use Standard strategy for balanced clear speed builds
- Set appropriate defensive thresholds for your survivability
- Configure buff maintenance for essential auras/buffs
- Monitor debug info to optimize skill priorities
- Bot stays in combat action until all monsters dead (0 remaining) for better enemy finding

## Advanced Features

### Boss Lock Mechanics (Aggressive Strategy)

The Aggressive strategy includes sophisticated boss detection and locking:

- Automatically detects Simulacrum bosses (Kosis, Omniphobia, DeliriumBoss)
- Scans 2x normal combat range to find higher-priority bosses
- Locks onto specific boss by name to prevent switching back to lower-priority bosses
- Disables repositioning when boss locked - stands still within 30 units
- Follows boss if he moves more than 30 units away
- Uses SingleTarget skills automatically when boss locked
- Safety timeouts: 30 seconds max lock without seeing boss, 150 units max distance

### Equipment Calibration

- Enable calibration mode in settings
- Use arrow keys or sliders to position equipment slots precisely
- Visual feedback shows all equipment positions
- Saves positions automatically

### Strategy Customization

- Combine different skill roles for complex rotations
- Use target priorities to focus on specific enemy types
- Adjust defensive thresholds based on your build's survivability
- Mix AreaDamage and SingleTarget skills for balanced clearing

## Architecture Notes

### Core Systems

- **GameConstants**: Centralized constants for boss names, entity metadata, item metadata
- **ICombatStrategy**: Interface for combat targeting strategies
- **Boss Lock System**: Priority-based boss detection with extended range scanning
- **Action System**: Combat, Explore, Loot, StoreItems, StartWave, LeaveMap actions
- **Smart Transitions**: Stays in combat until MonstersRemaining = 0, then explores

### Recent Improvements

- Boss lock now searches for specific locked boss by name, not just any boss
- Extended range (2x) boss priority scanning to detect distant high-priority bosses
- Repositioning disabled when boss locked for builds requiring stationary combat
- Combat action stays active until all monsters dead, even if 0 in range
- Null safety fixes for item paths in debug renderer

This bot provides intelligent automation for Simulacrum farming with special support for builds requiring stationary combat mechanics like Cast When Stunned.
