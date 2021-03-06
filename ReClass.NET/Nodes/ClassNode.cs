﻿using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Drawing;
using System.Linq;
using ReClassNET.AddressParser;
using ReClassNET.Extensions;
using ReClassNET.Memory;
using ReClassNET.UI;
using ReClassNET.Util;

namespace ReClassNET.Nodes
{
	public delegate void ClassCreatedEventHandler(ClassNode node);

	public class ClassNode : BaseContainerNode
	{
		public static event ClassCreatedEventHandler ClassCreated;

#if RECLASSNET64
		public static IntPtr DefaultAddress { get; } = (IntPtr)0x140000000;
#else
		public static IntPtr DefaultAddress { get; } = (IntPtr)0x400000;
#endif

		public override int MemorySize => Nodes.Sum(n => n.MemorySize);

		protected override bool ShouldCompensateSizeChanges => true;

		private NodeUuid uuid;
		public NodeUuid Uuid
		{
			get => uuid;
			set
			{
				Contract.Requires(value != null);

				uuid = value;
			}
		}

		public IntPtr Address
		{
			set
			{
				Contract.Ensures(AddressFormula != null);

				AddressFormula = value.ToString("X");
			}
		}

		public string AddressFormula { get; set; }

		public event NodeEventHandler NodesChanged;

		internal ClassNode(bool notifyClassCreated)
		{
			Contract.Ensures(AddressFormula != null);

			LevelsOpen.DefaultValue = true;

			Uuid = new NodeUuid(true);

			Address = DefaultAddress;

			if (notifyClassCreated)
			{
				ClassCreated?.Invoke(this);
			}
		}

		public static ClassNode Create()
		{
			Contract.Ensures(Contract.Result<ClassNode>() != null);

			return new ClassNode(true);
		}

		public override void GetUserInterfaceInfo(out string name, out Image icon)
		{
			throw new InvalidOperationException($"The '{nameof(ClassNode)}' node should not be accessible from the ui.");
		}

		public override bool CanHandleChildNode(BaseNode node)
		{
			switch (node)
			{
				case null:
				case ClassNode _:
				case VirtualMethodNode _:
					return false;
			}

			return true;
		}

		public override void Initialize()
		{
			AddBytes(IntPtr.Size);
		}

		public override Size Draw(ViewInfo view, int x, int y)
		{
			AddSelection(view, 0, y, view.Font.Height);

			var origX = x;
			var origY = y;

			x = AddOpenClose(view, x, y);

			var tx = x;

			x = AddIcon(view, x, y, Icons.Class, HotSpot.NoneId, HotSpotType.None);
			x = AddText(view, x, y, view.Settings.OffsetColor, 0, AddressFormula) + view.Font.Width;

			x = AddText(view, x, y, view.Settings.TypeColor, HotSpot.NoneId, "Class") + view.Font.Width;
			x = AddText(view, x, y, view.Settings.NameColor, HotSpot.NameId, Name) + view.Font.Width;
			x = AddText(view, x, y, view.Settings.ValueColor, HotSpot.NoneId, $"[{MemorySize}]") + view.Font.Width;
			x = AddComment(view, x, y);

			y += view.Font.Height;

			var size = new Size(x - origX, y - origY);

			if (LevelsOpen[view.Level])
			{
				var childOffset = tx - origX;

				var nv = view.Clone();
				nv.Level++;
				foreach (var node in Nodes)
				{
					// Draw the node if it is in the visible area.
					if (view.ClientArea.Contains(tx, y))
					{
						var innerSize = node.Draw(nv, tx, y);

						size = Utils.AggregateNodeSizes(size, innerSize.Extend(childOffset, 0));

						y += innerSize.Height;
					}
					else
					{
						// Otherwise calculate the height...
						var calculatedHeight = node.CalculateDrawnHeight(nv);

						// and check if the node area overlaps with the visible area...
						if (new Rectangle(tx, y, 9999999, calculatedHeight).IntersectsWith(view.ClientArea))
						{
							// then draw the node...
							var innerSize = node.Draw(nv, tx, y);

							size = Utils.AggregateNodeSizes(size, innerSize.Extend(childOffset, 0));

							y += innerSize.Height;
						}
						else
						{
							// or skip drawing and just use the calculated height.
							size = Utils.AggregateNodeSizes(size, new Size(0, calculatedHeight));

							y += calculatedHeight;
						}
					}
				}
			}

			return size;
		}

		public override int CalculateDrawnHeight(ViewInfo view)
		{
			if (IsHidden)
			{
				return HiddenHeight;
			}

			var height = view.Font.Height;
			if (LevelsOpen[view.Level])
			{
				var nv = view.Clone();
				nv.Level++;
				height += Nodes.Sum(n => n.CalculateDrawnHeight(nv));
			}
			return height;
		}

		public override void Update(HotSpot spot)
		{
			base.Update(spot);

			if (spot.Id == 0)
			{
				Offset = spot.Memory.Process.ParseAddress(spot.Text);

				AddressFormula = spot.Text;
			}
		}

		public void UpdateAddress(RemoteProcess process)
		{
			Contract.Requires(process != null);

			try
			{
				Offset = process.ParseAddress(AddressFormula);
			}
			catch (ParseException)
			{
				Offset = IntPtr.Zero;
			}
		}

		protected internal override void ChildHasChanged(BaseNode child)
		{
			NodesChanged?.Invoke(this);
		}
	}
}
