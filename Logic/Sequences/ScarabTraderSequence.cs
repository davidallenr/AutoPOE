using ExileCore.PoEMemory.Components;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Helpers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Windows.Forms;
using Stack = ExileCore.PoEMemory.Components.Stack;

namespace AutoPOE.Logic.Sequences
{
    public class ScarabTraderSequence : ISequence
    {
        private enum State
        {
            Start,
            GoToStash,
            OpenStash,
            StoreItems,
            WithdrawScarabs,
            GoToVendor,
            OpenVendor,
            SellScarabs,
            Done
        }

        private Random _random = new Random();
        private State _currentState = State.Start;
        private IEnumerator<bool> _sequenceCoroutine;
        private readonly Stopwatch _actionCooldown = new Stopwatch();
        private bool _isWaitingForNpcDialog = false;

        private int sellIndex = 0;

        public void Tick()
        {
            if (_sequenceCoroutine == null)
                _sequenceCoroutine = RunTraderSequence();

            // Only advance the coroutine if the cooldown is over
            if (_actionCooldown.IsRunning && _actionCooldown.ElapsedMilliseconds < 200)            
                return;
            
            _actionCooldown.Stop();

            if (!_sequenceCoroutine.MoveNext())
            {
                Core.IsBotRunning = false;
                _sequenceCoroutine = null;
            }
        }

        // The main sequence controller. It loops on a step until it returns true.
        private IEnumerator<bool> RunTraderSequence()
        {
            while (Core.IsBotRunning)
            {
                if (Core.GameController.IngameState.Data.ServerData.PlayerInventories[0].Inventory.InventorySlotItems.Count > 0)
                {
                    _currentState = State.GoToStash;
                    while (!GoToInteractable("Metadata/MiscellaneousObjects/Stash")) yield return false;

                    _currentState = State.OpenStash;
                    while (!OpenStash()) yield return false;

                    _currentState = State.StoreItems;
                    while (!StorePlayerInventory()) yield return false;
                }

                _currentState = State.GoToStash;
                while (!GoToInteractable("Metadata/MiscellaneousObjects/Stash")) yield return false;

                _currentState = State.OpenStash;
                while (!OpenStash()) yield return false;

                _currentState = State.WithdrawScarabs;
                while (!WithdrawScarabsFromStash()) yield return false;

                var playerInventory = Core.GameController.IngameState.Data.ServerData.PlayerInventories[0].Inventory;
                if (playerInventory.InventorySlotItems.Count == 0)
                    yield break;

                // Close stash before moving to vendor
                while (Core.GameController.IngameState.IngameUi.StashElement.IsVisible)
                {
                    Controls.UseKey(Keys.Escape);
                    StartCooldown();
                    yield return false;
                }

                _currentState = State.GoToVendor;
                while (!GoToInteractable(Core.Settings.Trader.NpcName)) yield return false;

                _currentState = State.OpenVendor;
                while (!OpenVendorMenu()) yield return false;

                _currentState = State.SellScarabs;
                while (!SellInventoryToVendor()) yield return false;

                _currentState = State.Done;
            }
        }

        private void StartCooldown()
        {
            _actionCooldown.Restart();
        }


        private bool GoToInteractable(string targetIdentifier)
        {
            var target = Core.GameController.EntityListWrapper.OnlyValidEntities
                .FirstOrDefault(e => e.Metadata.Contains(targetIdentifier) || e.RenderName.Contains(targetIdentifier));

            if (target == null)
            {
                Core.IsBotRunning = false;
                return true; 
            }

            if (Core.GameController.Player.GridPosNum.Distance(target.GridPosNum) < 10)
            {
                return true; 
            }

            var path = Core.Map.FindPath(Core.GameController.Player.GridPosNum, target.GridPosNum);
            path?.FollowPath();
            return false;
        }

        private bool OpenStash()
        {
            var stashElement = Core.GameController.IngameState.IngameUi.StashElement;
            if (stashElement.IsVisible)
            {
                return true;
            }

            var stashObject = Core.GameController.EntityListWrapper.OnlyValidEntities
                .FirstOrDefault(e => e.Metadata.Contains("Metadata/MiscellaneousObjects/Stash"));

            if (stashObject != null)
            {
                Controls.ClickScreenPos(Controls.GetScreenByGridPos(stashObject.GridPosNum));
                StartCooldown();
            }
            return false;
        }

        private bool StorePlayerInventory()
        {
            var playerInventory = Core.GameController.IngameState.Data.ServerData.PlayerInventories[0].Inventory;
            if (playerInventory.InventorySlotItems.Count == 0)
            {
                return true; 
            }

            var item = playerInventory.InventorySlotItems.First();
            var center = item.GetClientRect().Center;
            Controls.ClickScreenPos(new Vector2(center.X, center.Y), holdCtrl: true);
            StartCooldown();
            return false;
        }

        private bool WithdrawScarabsFromStash()
        {
            var playerInventory = Core.GameController.IngameState.Data.ServerData.PlayerInventories[0].Inventory;

            if (playerInventory.InventorySlotItems.Count >= 9)            
                return true;
            
            

            var scarabsToSell = Core.Settings.Trader.ScarabsToSell.Value.Split(',')
                .Select(s => s.Trim()).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();

            var visibleScarabs = Core.GameController.IngameState.IngameUi.StashElement?.VisibleStash?.VisibleInventoryItems
                .Select(item => new {
                    InvItem = item,
                    BaseName = item.Item.GetComponent<Base>()?.Name,
                    StackSize = item.Item.GetComponent<Stack>()?.Size ?? 0
                })
                .Where(x => x.BaseName != null && scarabsToSell.Contains(x.BaseName) && x.StackSize >= 180)
                .ToList();

            if (visibleScarabs == null || !visibleScarabs.Any())
            {
                Core.IsBotRunning = false;
                return true;
            }

            var scarabToWithdraw = visibleScarabs.First();
            var center = scarabToWithdraw.InvItem.GetClientRect().Center;
            Controls.ClickScreenPos(new Vector2(center.X, center.Y), holdCtrl: true);
            StartCooldown();
            return false;
        }

        private bool OpenVendorMenu()
        {
            if (Core.GameController.IngameState.IngameUi.SellWindow.IsVisible)
            {
                sellIndex = 0;
                _isWaitingForNpcDialog = false;
                return true;
            }

            //If we misclicked into the purchase window, close it.
            if (Core.GameController.IngameState.IngameUi.PurchaseWindow.IsVisible)
            {
                Controls.UseKey(Keys.Escape);
                return false;
            }

            var npcDialog = Core.GameController.IngameState.IngameUi.NpcDialog;
            if (npcDialog.IsVisible)
            {
                _isWaitingForNpcDialog = false;

                var sellOption = npcDialog.NpcLines.FirstOrDefault(o => o.Text.Contains("Sell Items"));
                if (sellOption != null)
                {
                    var center = sellOption.Element.Center;
                    Controls.ClickScreenPos(new Vector2(center.X, center.Y));
                    StartCooldown();
                }
                else
                {
                    Controls.UseKey(Keys.Escape);
                    StartCooldown();
                }
                return false;
            }

            if (_isWaitingForNpcDialog)            
                return false; 
            

            var vendor = Core.GameController.EntityListWrapper.OnlyValidEntities
                .FirstOrDefault(e => e.RenderName.Contains(Core.Settings.Trader.NpcName));

            if (vendor != null)
            {
                Controls.ClickScreenPos(Controls.GetScreenByGridPos(vendor.GridPosNum));
                _isWaitingForNpcDialog = true;
                StartCooldown();
            }

            return false;
        }

        private bool SellInventoryToVendor()
        {
            if(sellIndex > 100)
            {
                //EStop: clicked 100 times without succeeding.
                Core.IsBotRunning = false;
                return true;
            }

            var sellWindow = Core.GameController.IngameState.IngameUi.SellWindow;
            if (!sellWindow.IsVisible) return true;

            var playerInventoryItems = Core.GameController.IngameState.Data.ServerData.PlayerInventories[0].Inventory.InventorySlotItems;
            var itemsInOffer = sellWindow.YourOfferItems ?? new List<ExileCore.PoEMemory.Elements.InventoryElements.NormalInventoryItem>();
            if (playerInventoryItems.Count > itemsInOffer.Count)
            {
                var nextToSell = playerInventoryItems[sellIndex % playerInventoryItems.Count];
                sellIndex++;
                Controls.ClickScreenPos(new Vector2(nextToSell.GetClientRect().Center.X, nextToSell.GetClientRect().Center.Y), holdCtrl: true);
                StartCooldown();
                return false;
            }

            var acceptButton = sellWindow.AcceptButton;
            if (acceptButton.IsVisible)
            {
                var center = acceptButton.GetClientRect().Center;
                Controls.ClickScreenPos(new Vector2(center.X, center.Y));
                StartCooldown();
            }
            return false; 
        }

        public void Render()
        {
            Core.Graphics.DrawText($"State: {_currentState}", new Vector2(115, 115), SharpDX.Color.White);
        }
    }
}