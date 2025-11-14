# AutoPOE - Advanced Simulacrum Farming Bot for Exile API

An intelligent, strategy-based farming bot for Path of Exile's Simulacrum encounters. This bot features sophisticated combat strategies, intelligent skill management, and automated incubator application for maximum profit per hour.

## 🎯 **Key Features**

- **Strategic Combat System**: Multiple combat strategies with different targeting priorities
- **Intelligent Skill Management**: Role-based skill system with priority targeting
- **Automated Incubator Application**: Automatically applies incubators from stash to equipment
- **Debug Visualization**: Comprehensive overlay showing bot status, targeting, and combat info
- **Equipment Calibration**: Visual calibration system for precise equipment slot positioning
- **Stable Navigation**: Advanced pathfinding with anti-stuck mechanisms

## 🚀 **Combat Strategies**

### **Standard Strategy**

- **Best For**: Balanced builds, general farming
- **Behavior**: Prioritizes rare and unique monsters first, then closest enemies
- **Target Selection**: Focuses on highest value targets for efficient clearing

### **Aggressive Strategy**

- **Best For**: High DPS builds, fast clearing
- **Behavior**: Targets groups of enemies for maximum clear speed
- **Target Selection**: Finds center of largest enemy groups with target stability to reduce cursor jitter

## ⚙️ **Skill Configuration**

### **Skill Roles** (What each skill does)

- **PrimaryDamage**: Your main attack skill
- **AreaDamage**: AOE skills for clearing groups
- **SingleTarget**: High damage skills for rares/bosses
- **Defensive**: Guard skills and panic buttons
- **Buff**: Auras, buffs, and toggle skills
- **Movement**: Navigation and traversal skills

### **Target Priorities** (What the skill targets)

- **HighestThreat**: Rare/Unique monsters first, then closest
- **ClosestEnemy**: Always targets nearest enemy
- **RareFirst**: Only targets rare monsters
- **UniqueFirst**: Only targets unique monsters
- **MostEnemies**: Targets center of largest group
- **SelfTarget**: For self-cast skills (buffs, etc)
- **AllyTarget**: For mercenary/minion buffs

### **Cast Types** (How the skill is used)

- **TargetMonster**: Click on monsters
- **TargetSelf**: Cast on player position
- **TargetMercenary**: Cast on mercenary/allies
- **TargetGround**: Ground targeted skills
- **DoNotUse**: Disable the skill

## 🔧 **Setup Instructions**

### **1. Basic Configuration**

1. Install the plugin in your ExileAPI
2. Configure at least one Movement skill (required for navigation)
3. Set up your combat skills with appropriate roles and priorities
4. **Important**: Include at least one blink skill (Frostblink/Flame Dash) to help with terrain navigation

### **2. Skill Setup Examples**

**Typical Melee Build:**

- Skill1 (Q): Main Attack → PrimaryDamage, HighestThreat, Priority 8
- Skill2 (W): Movement → Movement, SelfTarget, Priority 5
- Skill3 (E): Guard Skill → Defensive, SelfTarget, Priority 10
- Skill4 (R): Aura → Buff, SelfTarget, Priority 6

**Typical Spell Build:**

- Skill1 (Q): Main Spell → AreaDamage, MostEnemies, Priority 8
- Skill2 (W): Single Target → SingleTarget, RareFirst, Priority 7
- Skill3 (E): Movement → Movement, SelfTarget, Priority 5
- Skill4 (R): Curse → AreaDamage, MostEnemies, Priority 6

### **3. Combat Settings**

- **Combat Strategy**: Choose Standard (balanced) or Aggressive (fast clearing)
- **Defensive Threshold**: Health % to trigger defensive skills (default 50%)
- **Buff Maintenance**: Automatically maintain important buffs (recommended: ON)
- **Focus Fire**: Concentrate on single targets vs spread damage

### **4. Incubator Setup**

- Store incubators in your stash dump tab
- Enable "Use Incubators" setting
- Bot will automatically apply them to equipment during stash visits

## 🎮 **How To Use**

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

## 🐛 **Debug Features**

### **Debug Overlay Information**

- **Bot Status**: Running state, focus detection, action timing
- **Location Info**: Current area, hideout detection
- **Combat Info**: Active strategy, target selection, skill usage
- **Incubator Status**: Detection and application status

### **Visual Debug Options**

- **Draw Inventory**: Shows item boxes and equipment slots
- **Combat Debug**: Displays targeting and strategy information
- **Stash Debug**: Shows stash contents and incubator detection
- **Equipment Calibration**: Visual positioning system for equipment slots

## ⚠️ **Important Notes**

### **Skill Configuration Tips**

- **Movement Skills**: Set these as "Movement" role, never "DoNotUse"
- **Buff Skills**: Use the "Buff Name" field to prevent unnecessary recasting
- **Priority System**: Higher numbers = higher priority (1-10 scale)
- **Strategy Priority**: Skills with priority 8+ are considered essential

### **Common Issues**

- **Bot gets stuck**: Ensure you have blink skills configured as movement
- **Skills not casting**: Check skill names match exactly (case sensitive)
- **Poor targeting**: Adjust Target Priority settings for your build
- **Equipment issues**: Use calibration mode to set precise positions

### **Performance Tips**

- Use Aggressive strategy for fast clearing builds
- Set appropriate defensive thresholds for your survivability
- Configure buff maintenance for essential auras/buffs
- Monitor debug info to optimize skill priorities

## 📈 **Advanced Features**

### **Equipment Calibration**

- Enable calibration mode in settings
- Use arrow keys or sliders to position equipment slots precisely
- Visual feedback shows all equipment positions
- Saves positions automatically

### **Strategy Customization**

- Combine different skill roles for complex rotations
- Use target priorities to focus on specific enemy types
- Adjust defensive thresholds based on your build's survivability
- Mix AreaDamage and SingleTarget skills for balanced clearing

This bot represents a significant evolution in automated Path of Exile farming, providing the intelligence and flexibility needed for reliable, profitable Simulacrum runs.
