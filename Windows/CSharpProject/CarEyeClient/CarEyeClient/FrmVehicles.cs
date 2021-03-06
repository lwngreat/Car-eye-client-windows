﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CarEyeClient.Model;
using CarEyeClient.Properties;
using CarEyeClient.Utils;

namespace CarEyeClient
{
	/// <summary>
	/// 车辆详情列表窗口
	/// </summary>
	public partial class FrmVehicles : CarEyeClient.FrmChild
	{
		/// <summary>
		/// 主窗体
		/// </summary>
		private FrmMain mParent;
		/// <summary>
		/// 上次上报时间
		/// </summary>
		private DateTime mPrvReportTime;

		public FrmVehicles(FrmMain aParent)
		{
			InitializeComponent();
			// 禁止自动生成属性列
			this.dgvVechiles.AutoGenerateColumns = false;
			mParent = aParent;
		}

		/// <summary>
		/// 窗体载入过程
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void FrmVehicles_Load(object sender, EventArgs e)
		{
			if (!bgkReport.IsBusy)
			{
				// 开启自动点名线程
				mPrvReportTime = DateTime.Now;
				bgkReport.RunWorkerAsync();
			}
		}

		/// <summary>
		/// 添加一个车辆到车辆列表中
		/// </summary>
		/// <param name="aVehicle"></param>
		public void AddVehicle(JsonLastPosition aVehicle)
		{
			List<JsonLastPosition> vehicles;
			if (this.dgvVechiles.DataSource is List<JsonLastPosition>)
			{
				vehicles = this.dgvVechiles.DataSource as List<JsonLastPosition>;
			}
			else
			{
				vehicles = new List<JsonLastPosition>();
			}

			int tmpIndex = vehicles.FindIndex(x => x.VehicleIndex == aVehicle.VehicleIndex);
			if (tmpIndex < 0)
			{
				vehicles.Insert(0, aVehicle);
			}
			else
			{
				vehicles[tmpIndex] = aVehicle;
				this.dgvVechiles.Refresh();
				return;
			}
			
			this.dgvVechiles.DataSource = null;
			this.dgvVechiles.DataSource = vehicles;
			this.dgvVechiles.Refresh();
		}

		/// <summary>
		/// 双击进行定位
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void dgvVechiles_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
		{
			if (e.RowIndex < 0)
			{
				return;
			}

			JsonLastPosition location = this.dgvVechiles.Rows[e.RowIndex].DataBoundItem as JsonLastPosition;
			if (location == null)
			{
				return;
			}
			mParent.LocatedVehicle(location);
		}

		/// <summary>
		/// 获取选中的车辆信息, 未选中返回null
		/// </summary>
		/// <returns></returns>
		private JsonLastPosition GetSelectedVehicle()
		{
			if (this.dgvVechiles.SelectedRows.Count < 1)
			{
				return null;
			}

			return this.dgvVechiles.SelectedRows[0].DataBoundItem as JsonLastPosition;
		}

		/// <summary>
		/// 点名, 获取最新位置信息
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void mnuRequest_Click(object sender, EventArgs e)
		{
			var selVehicle = GetSelectedVehicle();
			if (selVehicle == null)
			{
				return;
			}

			var task = Task.Factory.StartNew(() =>
			{
				var lastLocation = UrlApiHelper.GetLastLocation(selVehicle.TerminalId);
				if (lastLocation == null)
				{
					GuiHelper.MsgBox("服务器连接异常...");
				}
				else if (lastLocation.Status != 0)
				{
					GuiHelper.MsgBox("点名失败: " + lastLocation.Message);
				}
				else
				{
					this.dgvVechiles.Invoke(new Action<JsonLastPosition>(AddVehicle), lastLocation);
					mParent.LocatedVehicle(lastLocation);
				}
			});
		}

		/// <summary>
		/// 轨迹回放功能
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void mnuTrack_Click(object sender, EventArgs e)
		{
			var selVehicle = GetSelectedVehicle();
			if (selVehicle == null)
			{
				return;
			}

			DlgTrackRequest tmpDlg = new DlgTrackRequest(selVehicle.TerminalId);
			if (tmpDlg.ShowDialog() != DialogResult.OK)
			{
				return;
			}

			mParent.PlayHistory(tmpDlg.History);
		}

		/// <summary>
		/// 开启视频预览
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void mnuDVR_Click(object sender, EventArgs e)
		{
			var selVehicle = GetSelectedVehicle();
			if (selVehicle == null)
			{
				return;
			}

			DlgDVRRequest tmpDlg = new DlgDVRRequest(selVehicle.TerminalId);
			if (tmpDlg.ShowDialog() != DialogResult.OK)
			{
				return;
			}

			mParent.EnableDVR(tmpDlg.TerminalId, tmpDlg.Channel);
		}

		/// <summary>
		/// 自动点名工作线程
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void bgkReport_DoWork(object sender, DoWorkEventArgs e)
		{
			while (!bgkReport.CancellationPending)
			{
				Thread.Sleep(500);
				Application.DoEvents();
				DateTime now = DateTime.Now;
				if ((now - mPrvReportTime).TotalSeconds < Settings.Default.AutoReportInterval)
				{
					continue;
				}

				mPrvReportTime = now;
				var vehicles = (this.dgvVechiles.DataSource as List<JsonLastPosition>).ToArray();
				if (vehicles == null || vehicles.Length < 1)
				{
					continue;
				}

				foreach (var vehicle in vehicles)
				{
					if (bgkReport.CancellationPending)
					{
						// 系统取消退出
						return;
					}
					var lastLocation = UrlApiHelper.GetLastLocation(vehicle.TerminalId);
					if (lastLocation != null && lastLocation.Status == 0)
					{
						this.dgvVechiles.Invoke(new Action<JsonLastPosition>(AddVehicle), lastLocation);
					}
				}
			}
		}

		/// <summary>
		/// 关闭窗体时取消后台任务
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void FrmVehicles_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (bgkReport.IsBusy)
			{
				bgkReport.CancelAsync();
			}
		}
	}
}
