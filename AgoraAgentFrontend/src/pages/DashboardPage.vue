<template>
  <q-page class="dashboard-page">
    <div class="app-shell row no-wrap items-stretch">

      <!-- Sidebar -->
      <aside class="sidebar col-12 col-md-2 q-pa-sm">
        <div class="brand-wrap row items-center no-wrap">
          <div class="logo-crest">
            <svg width="40" height="40" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
              <circle cx="12" cy="12" r="11" stroke="rgba(0,255,255,0.12)" stroke-width="1" fill="rgba(255,255,255,0.02)"/>
              <text x="12" y="16" text-anchor="middle" font-size="12" fill="var(--accent)">A</text>
            </svg>
          </div>
          <div class="brand-text">
            <div class="brand-title">AGORA</div>
            <div class="brand-sub">Agent</div>
          </div>
        </div>

        <nav class="q-mt-md">
          <q-list dense separator>
            <q-item clickable active class="nav-item">
              <q-item-section avatar>
                <q-icon name="dashboard" />
              </q-item-section>
              <q-item-section>DASHBOARD</q-item-section>
            </q-item>
            <q-item clickable class="nav-item">
              <q-item-section avatar>
                <q-icon name="account_balance_wallet" />
              </q-item-section>
              <q-item-section>MON PORTEFEUILLE</q-item-section>
            </q-item>
            <q-item clickable class="nav-item">
              <q-item-section avatar>
                <q-icon name="insights" />
              </q-item-section>
              <q-item-section>STRATÉGIES</q-item-section>
            </q-item>
            <q-item clickable class="nav-item">
              <q-item-section avatar>
                <q-icon name="assessment" />
              </q-item-section>
              <q-item-section>RAPPORTS</q-item-section>
            </q-item>
          </q-list>
        </nav>

        <div class="resources q-mt-xl">
          <div class="resources-title">RESOURCES</div>
          <q-list dense>
            <q-item clickable>
              <q-item-section avatar><q-icon name="link" /></q-item-section>
              <q-item-section>Docs</q-item-section>
            </q-item>
            <q-item clickable>
              <q-item-section avatar><q-icon name="support" /></q-item-section>
              <q-item-section>Support</q-item-section>
            </q-item>
          </q-list>
        </div>
      </aside>

      <!-- Main -->
      <main class="main-area col-12 col-md-10 q-pa-md">

        <!-- Top header -->
        <header class="topbar row items-center justify-between q-px-sm q-py-xs">
          <div class="title">DASHBOARD &gt; AGENT #001</div>

          <div class="row items-center q-gutter-sm">
            <div class="conn-indicator" :class="{ connected: isConnected }" title="SignalR status">
              <span class="dot" />
            </div>
            <q-btn flat color="white" label="Connect" @click="connect" />
            <q-btn outline color="accent" icon="flash_on" label="Simuler Trade Arc" :loading="simLoading" @click="simulateTrade" />
          </div>
        </header>

        <!-- Content -->
        <div class="content q-mt-md">
          <div class="row q-col-gutter-md">

            <!-- Wallet / Bond Balance -->
            <div class="col-12 col-md-4">
              <q-card class="glass-card wallet-card">
                <q-card-section>
                  <div class="card-header muted">FONDS DISPONIBLES</div>
                  <div class="card-sub">(BOND BALANCE)</div>

                  <div class="balance-row q-mt-md">
                    <div class="balance-value">{{ formattedBalance }}</div>
                    <div class="usdc-chip">USDC</div>
                  </div>

                  <svg class="sparkline" viewBox="0 0 100 30" preserveAspectRatio="none" v-if="sparkPath">
                    <path :d="sparkPath" stroke="var(--accent)" stroke-width="1.6" fill="none" stroke-linecap="round" stroke-linejoin="round" opacity="0.95" />
                  </svg>
                </q-card-section>
              </q-card>
            </div>

            <!-- Transactions table -->
            <div class="col-12 col-md-8">
              <q-card class="glass-card table-card">
                <q-card-section class="card-head">
                  <div class="row items-center justify-between">
                    <div class="text-h6">FLUX DE TRANSACTIONS TEMPS RÉEL (ARC NETWORK)</div>
                    <div>
                      <!-- Kept small connect control here as redundancy -->
                      <q-btn flat label="Connect" @click="connect" />
                    </div>
                  </div>
                </q-card-section>

                <q-table
                  :rows="transactions"
                  :columns="columns"
                  row-key="id"
                  dense
                  flat
                  :row-class="rowClass"
                >
                  <template v-slot:body-cell-createdAt="props">
                    <div class="mono">{{ formatTime(props.row.createdAt) }}</div>
                  </template>

                  <template v-slot:body-cell-action="props">
                    <div class="row items-center">
                      <q-icon :name="props.row.action === 'Buy' ? 'arrow_upward' : 'arrow_downward'" :color="props.row.action === 'Buy' ? 'accent' : 'negative'" />
                      <span class="action-text q-ml-xs">{{ props.row.action }}</span>
                    </div>
                  </template>

                  <template v-slot:body-cell-status="props">
                    <div :class="['glass-badge', props.row.status?.toLowerCase().includes('success') ? 'success' : (props.row.status?.toLowerCase().includes('pending') ? 'pending' : '')]">
                      {{ props.row.status }}
                    </div>
                  </template>

                  <template v-slot:body-cell-arcLink="props">
                    <q-btn flat round dense icon="link" @click="openArcLink(props.row.txHash)" />
                  </template>
                </q-table>
              </q-card>
            </div>
          </div>
        </div>
      </main>
    </div>
  </q-page>
</template>

<script lang="ts">
import { defineComponent, computed, onMounted, ref, watch } from 'vue';
import { useAgentStore } from '../stores/agentStore';
import axios from 'axios';
import { HubConnectionState } from '@microsoft/signalr';
import type { components } from '../api/models';

type TradingTransactionDto = components['schemas']['TradingTransactionDto'];

export default defineComponent({
  setup() {
    const store = useAgentStore();
    const transactions = computed<TradingTransactionDto[]>(() => (store.transactions ?? []) as TradingTransactionDto[]);
    const displayBalance = computed(() => store.currentAgent?.bondBalance ?? 0);
    const simLoading = ref(false);

    const columns = [
      { name: 'createdAt', label: 'TIME', field: 'createdAt', sortable: true },
      { name: 'action', label: 'ACTION', field: 'action' },
      { name: 'amount', label: 'AMOUNT', field: 'amount' },
      { name: 'priceAtTrade', label: 'PRICE', field: 'priceAtTrade' },
      { name: 'status', label: 'STATUS', field: 'status' },
      { name: 'arcLink', label: 'ARC LINK', field: 'txHash' },
    ];

    const recentTxs = ref(new Set<string>());

    const isConnected = computed(() => {
      const conn = store.connection;
      if (!conn) return false;
      return conn.state === HubConnectionState.Connected;
    });

    // Watch for newly arrived transactions and mark them briefly for animation
    watch(transactions, (newArr: TradingTransactionDto[] = [], oldArr: TradingTransactionDto[] = []) => {
      const oldIds = new Set(oldArr.map(t => t.id));
      newArr.forEach((t: TradingTransactionDto) => {
        if (!oldIds.has(t.id)) {
          recentTxs.value.add(t.id);
          setTimeout(() => recentTxs.value.delete(t.id), 3500);
        }
      });
    }, { deep: true });

    const rowClass = (row: TradingTransactionDto) => {
      return recentTxs.value.has(row.id) ? 'tx-new' : '';
    };

    const formatTime = (iso: string) => {
      if (!iso) return '';
      try {
        return new Date(iso).toLocaleTimeString([], { hour12: false });
      } catch {
        return iso;
      }
    };

    const openArcLink = (txHash?: string) => {
      if (!txHash) return;
      const url = `https://arcscan.example/tx/${txHash}`;
      window.open(url, '_blank');
    };

    const connect = async () => {
      await store.connectSignalR();
    };

    const simulateTrade = async () => {
      simLoading.value = true;
      try {
        const base = (import.meta.env.VITE_API_URL as string) || 'http://localhost:5000';
        await axios.post(`${base}/api/demo/trigger-trade`);
      } catch (err) {
        console.error('Error triggering demo trade', err);
      } finally {
        simLoading.value = false;
      }
    };

    const formattedBalance = computed(() => {
      const b = Number(displayBalance.value ?? 0);
      return `${b.toFixed(2)} USDC`;
    });

    // Sparkline: simulate from last few transaction amounts
    const sparkPath = computed(() => {
      const vals = (store.transactions ?? []).slice(0, 12).map((t: TradingTransactionDto) => Number(t.amount) || 0);
      if (vals.length === 0) return '';
      const max = Math.max(...vals);
      const min = Math.min(...vals);
      const pts = vals.map((v, i) => {
        const x = (i / Math.max(1, vals.length - 1)) * 100;
        const y = max === min ? 50 : 100 - ((v - min) / (max - min) * 100);
        return { x, y };
      });
      return pts.map((p, i) => `${i === 0 ? 'M' : 'L'} ${p.x} ${p.y}`).join(' ');
    });

    onMounted(() => {
      void connect();
    });

    return { transactions, columns, formattedBalance, connect, simulateTrade, simLoading, isConnected, rowClass, formatTime, openArcLink, sparkPath };
  },
});
</script>

<style scoped>
@import url('https://fonts.googleapis.com/css2?family=Inter:wght@300;400;600;800&display=swap');

:root { }
.dashboard-page {
  --bg: #121212;
  --card-bg: rgba(255,255,255,0.02);
  --accent: #00FFFF;
  --accent-rgb: 0,255,255;
  --success: #00ff7a;
  --pending: #ffb84d;
  font-family: 'Inter', system-ui, -apple-system, 'Segoe UI', Roboto, 'Helvetica Neue', Arial;
  background: linear-gradient(180deg, #0f0f10 0%, var(--bg) 100%);
  color: #e6eef2;
  min-height: 100vh;
}

.app-shell { gap: 20px; }
.sidebar {
  background: var(--card-bg);
  border-radius: 12px;
  padding: 16px;
  min-height: 88vh;
  backdrop-filter: blur(8px);
  border: 1px solid rgba(var(--accent-rgb), 0.06);
  box-shadow: 0 6px 20px rgba(0,0,0,0.6);
}
.brand-wrap { gap: 12px; }
.brand-title { font-weight: 800; letter-spacing: 1px; color: var(--accent); }
.brand-sub { font-size: 12px; color: #9aa3a8; }
.nav-item { margin-top: 8px; border-radius: 8px; }
.nav-item.q-item--active { background: linear-gradient(90deg, rgba(var(--accent-rgb),0.06), rgba(255,255,255,0.01)); border-left: 3px solid var(--accent); }
.resources-title { font-size: 12px; color: #9aa3a8; margin-bottom: 6px; }

.main-area { }
.topbar { background: transparent; color: #dbeaf0; align-items: center; }
.topbar .title { font-weight: 700; letter-spacing: 1px; color: #cfefff; }
.conn-indicator { width: 36px; display:flex; align-items:center; justify-content:center; }
.conn-indicator .dot { width: 10px; height: 10px; border-radius: 50%; background: rgba(255,255,255,0.08); box-shadow: none; transition: all .2s; }
.conn-indicator.connected .dot { background: var(--success); box-shadow: 0 0 10px rgba(0,255,122,0.24); animation: pulse 1.6s infinite; }
@keyframes pulse { 0% { transform: scale(1); opacity: 1 } 50% { transform: scale(1.25); opacity: .85 } 100% { transform: scale(1); opacity: 1 } }

.glass-card { background: var(--card-bg); border-radius: 14px; border: 1px solid rgba(var(--accent-rgb), 0.06); box-shadow: 0 8px 30px rgba(2,6,23,0.7); backdrop-filter: blur(10px); }
.wallet-card { padding: 18px; }
.card-header { color: #9fb6b9; font-weight: 600; font-size: 12px; }
.card-sub { color: #7f8b8f; font-size: 12px; }
.balance-row { display:flex; align-items:center; gap:12px; margin-top: 12px; }
.balance-value { font-size: 34px; font-weight: 800; color: #e6fff7; }
.usdc-chip { padding:6px 10px; border-radius: 12px; font-size: 12px; color:#0b0b0b; background: linear-gradient(180deg, rgba(255,255,255,0.95), rgba(255,255,255,0.85)); }

.sparkline { width: 100%; height: 36px; margin-top: 12px; opacity: 0.9; }

.table-card { padding: 12px; }
.table-card .card-head { padding-bottom: 6px; }
.glass-badge { display:inline-block; padding:6px 10px; border-radius:999px; font-weight:600; font-size:12px; backdrop-filter: blur(6px); background: rgba(255,255,255,0.02); border: 1px solid rgba(255,255,255,0.04); }
.glass-badge.success { border-color: rgba(0,255,122,0.18); color: var(--success); box-shadow: 0 0 10px rgba(0,255,122,0.06); }
.glass-badge.pending { border-color: rgba(255,184,77,0.12); color: var(--pending); box-shadow: 0 0 12px rgba(255,184,77,0.04); }

.mono { font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, 'Roboto Mono', 'Courier New', monospace; font-size: 13px; }
.tx-new { animation: neonFlash 1.4s ease; box-shadow: 0 0 18px rgba(var(--accent-rgb),0.08); border-left: 3px solid rgba(var(--accent-rgb),0.18); }
@keyframes neonFlash { 0% { transform: translateY(-2px); } 50% { transform: translateY(0); box-shadow: 0 0 30px rgba(var(--accent-rgb),0.12); } 100% { transform: translateY(-1px); } }

.action-text { font-weight: 600; }

/* responsive tweaks */
@media (max-width: 959px) {
  .sidebar { display:none; }
}
</style>
