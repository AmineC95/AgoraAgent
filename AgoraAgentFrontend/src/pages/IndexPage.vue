<template>
  <q-page class="q-pa-lg">
    <div class="row q-col-gutter-lg">
      <!-- ── Treasury card ─────────────────────────────────────────────────── -->
      <div class="col-12 col-md-4">
        <q-card dark flat bordered class="card-glow full-height">
          <q-card-section>
            <div class="row items-center no-wrap q-mb-sm">
              <q-icon name="account_balance_wallet" color="primary" size="22px" class="q-mr-sm" />
              <div>
                <div class="text-overline text-grey-5" style="letter-spacing: 2px">AVAILABLE TREASURY</div>
                <div class="text-caption text-grey-6">Agent Bond Balance</div>
              </div>
              <q-space />
              <q-btn flat round dense icon="refresh" size="sm" color="grey-5" @click="refresh" :loading="refreshing" />
            </div>

            <div class="row items-baseline q-mt-md">
              <div class="text-h3 text-weight-bold text-white">{{ balanceDisplay }}</div>
              <div class="text-h6 text-primary q-ml-sm">USDC</div>
            </div>

            <div class="row items-center q-mt-sm">
              <q-icon :name="(store.currentAgent?.status ?? store.agent?.status) === 'Active' ? 'circle' : 'circle'"
                :color="(store.currentAgent?.status ?? store.agent?.status) === 'Active' ? 'positive' : 'grey-5'"
                size="10px" class="q-mr-xs" />
              <span class="text-caption text-grey-5">
                Agent {{ store.currentAgent?.status ?? store.agent?.status ?? 'Loading...' }}
              </span>
            </div>

            <!-- Mini sparkline -->
            <div v-if="sparkPoints.length > 1" class="q-mt-md">
              <svg viewBox="0 0 100 28" preserveAspectRatio="none" style="width:100%; height:48px">
                <defs>
                  <linearGradient id="sparkGrad" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="0%" stop-color="var(--q-primary)" stop-opacity="0.4" />
                    <stop offset="100%" stop-color="var(--q-primary)" stop-opacity="0" />
                  </linearGradient>
                </defs>
                <path :d="sparkFill" fill="url(#sparkGrad)" />
                <path :d="sparkLine" stroke="var(--q-primary)" stroke-width="1.5" fill="none" stroke-linecap="round"
                  stroke-linejoin="round" />
              </svg>
              <div class="text-caption text-grey-6 text-right">Last {{ sparkPoints.length }} trades</div>
            </div>
          </q-card-section>

          <!-- Agent address -->
          <q-separator dark />
          <q-card-section class="q-py-sm">
            <div class="text-caption text-grey-6 ellipsis">
              <q-icon name="account_circle" size="14px" class="q-mr-xs" />
              {{ store.currentAgent?.publicAddress ?? store.agent?.publicAddress ?? 'Fetching address...' }}
            </div>
          </q-card-section>
        </q-card>
      </div>

      <!-- ── Live activity table ────────────────────────────────────────────── -->
      <div class="col-12 col-md-8">
        <q-card dark flat bordered class="full-height">
          <q-card-section class="q-pb-xs">
            <div class="row items-center">
              <div>
                <div class="text-h6 text-weight-bold" style="letter-spacing: 1px">
                  LIVE ON-CHAIN ACTIVITY
                  <q-badge color="primary" class="q-ml-sm" style="letter-spacing:1px">ARC TESTNET</q-badge>
                </div>
                <div class="text-caption text-grey-5 q-mt-xs">
                  Autonomous trading decisions powered by Llama3 and executed on Arc Testnet.
                </div>
              </div>
              <q-space />
              <q-chip :color="store.isConnected ? 'positive' : 'grey-7'" text-color="white"
                :icon="store.isConnected ? 'wifi' : 'wifi_off'" size="sm" dense>
                {{ store.isConnected ? 'LIVE' : 'OFFLINE' }}
              </q-chip>
            </div>
          </q-card-section>

          <q-separator dark />

          <q-card-section class="q-pa-none">
            <q-table :rows="store.transactions" :columns="columns" row-key="id" flat dark dense
              :pagination="{ rowsPerPage: 12 }"
              :no-data-label="store.loading ? 'Loading...' : 'No trades yet. Hit EXECUTE AI TRADE to begin.'"
              class="activity-table">
              <!-- Time column -->
              <template #body-cell-createdAt="props">
                <q-td :props="props" class="text-mono text-grey-4">
                  {{ formatTime(props.row.createdAt) }}
                </q-td>
              </template>

              <!-- Action column -->
              <template #body-cell-action="props">
                <q-td :props="props">
                  <q-chip :color="props.row.action === 'Buy' ? 'teal-9' : 'deep-orange-9'"
                    :icon="props.row.action === 'Buy' ? 'arrow_upward' : 'arrow_downward'" text-color="white" dense
                    size="sm">
                    {{ props.row.action ?? '-' }}
                  </q-chip>
                </q-td>
              </template>

              <!-- Amount column -->
              <template #body-cell-amount="props">
                <q-td :props="props" class="text-mono text-white">
                  {{ formatAmount(props.row.amount) }}
                </q-td>
              </template>

              <!-- Price column -->
              <template #body-cell-priceAtTrade="props">
                <q-td :props="props" class="text-mono text-grey-4">
                  {{ props.row.priceAtTrade != null ? '$' + Number(props.row.priceAtTrade).toFixed(4) : '-' }}
                </q-td>
              </template>

              <!-- Status column -->
              <template #body-cell-status="props">
                <q-td :props="props">
                  <q-chip v-if="isSuccess(props.row.status)" color="positive" icon="check_circle" text-color="white"
                    dense size="sm">
                    Success
                  </q-chip>
                  <q-chip v-else-if="isPending(props.row.status)" color="warning" icon="hourglass_empty"
                    text-color="dark" dense size="sm">
                    Pending
                  </q-chip>
                  <q-chip v-else color="negative" icon="error" text-color="white" dense size="sm">
                    Failed
                  </q-chip>
                </q-td>
              </template>

              <!-- Arc explorer link -->
              <template #body-cell-arcLink="props">
                <q-td :props="props">
                  <q-btn v-if="props.row.txHash" flat round dense icon="open_in_new" size="xs" color="primary"
                    @click="openExplorer(props.row.txHash)">
                    <q-tooltip>View on Arc Explorer</q-tooltip>
                  </q-btn>
                  <span v-else class="text-grey-7">-</span>
                </q-td>
              </template>

              <!-- New-tx highlight via row class -->
              <template #body="props">
                <q-tr :props="props" :class="recentIds.has(props.row.id ?? '') ? 'row-highlight' : ''">
                  <q-td key="createdAt" :props="props" class="text-mono text-grey-4">
                    {{ formatTime(props.row.createdAt) }}
                  </q-td>
                  <q-td key="action" :props="props">
                    <q-chip :color="props.row.action === 'Buy' ? 'teal-9' : 'deep-orange-9'"
                      :icon="props.row.action === 'Buy' ? 'arrow_upward' : 'arrow_downward'" text-color="white" dense
                      size="sm">
                      {{ props.row.action ?? '-' }}
                    </q-chip>
                  </q-td>
                  <q-td key="amount" :props="props" class="text-mono text-white">
                    {{ formatAmount(props.row.amount) }}
                  </q-td>
                  <q-td key="priceAtTrade" :props="props" class="text-mono text-grey-4">
                    {{ props.row.priceAtTrade != null ? '$' + Number(props.row.priceAtTrade).toFixed(4) : '-' }}
                  </q-td>
                  <q-td key="status" :props="props">
                    <q-chip v-if="isSuccess(props.row.status)" color="positive" icon="check_circle" text-color="white"
                      dense size="sm">Success</q-chip>
                    <q-chip v-else-if="isPending(props.row.status)" color="warning" icon="hourglass_empty"
                      text-color="dark" dense size="sm">Pending</q-chip>
                    <q-chip v-else color="negative" icon="error" text-color="white" dense size="sm">Failed</q-chip>
                  </q-td>
                  <q-td key="arcLink" :props="props">
                    <q-btn v-if="props.row.txHash" flat round dense icon="open_in_new" size="xs" color="primary"
                      @click="openExplorer(props.row.txHash)">
                      <q-tooltip>View on Arc Explorer</q-tooltip>
                    </q-btn>
                    <span v-else class="text-grey-7">-</span>
                  </q-td>
                </q-tr>
              </template>
            </q-table>
          </q-card-section>
        </q-card>
      </div>
    </div>
  </q-page>
</template>

<script setup lang="ts">
/**
 * IndexPage - Dashboard
 *
 * This page displays the agent's treasury and live on-chain activity. It uses the `agentStore`
 * Pinia store as the single source of truth and relies on a SignalR connection to receive
 * real-time `TradeUpdated` and `BalanceUpdated` events. The `refresh()` function fetches
 * the agent and transactions from the backend and is called on mount and after trades.
 *
 * NOTE (Production Architecture): Consider paginating transactions on the backend and adding rate-limiting
 * to avoid overwhelming the client when the dataset grows.
 */
import { ref, computed, watch, onMounted } from 'vue';
import { useAgentStore } from '../stores/agentStore';
import type { TradingTransactionDto } from '../stores/agentStore';

const store = useAgentStore();
const refreshing = ref(false);

// ── Column definitions ────────────────────────────────────────────────────────
const columns = [
  { name: 'createdAt', label: 'TIME', field: 'createdAt', align: 'left' as const, sortable: true },
  { name: 'action', label: 'ACTION', field: 'action', align: 'left' as const },
  { name: 'amount', label: 'AMOUNT', field: 'amount', align: 'right' as const, sortable: true },
  { name: 'priceAtTrade', label: 'PRICE', field: 'priceAtTrade', align: 'right' as const },
  { name: 'status', label: 'STATUS', field: 'status', align: 'center' as const },
  { name: 'arcLink', label: 'EXPLORER', field: 'txHash', align: 'center' as const },
];

// ── Formatters ────────────────────────────────────────────────────────────────
function formatTime(iso?: string | null): string {
  if (!iso) return '-';
  try {
    // Ensure the ISO is interpreted as UTC then display in local timezone with date
    const s = iso.endsWith('Z') || iso.includes('+') || /Z$/i.test(iso) ? iso : iso + 'Z';
    return new Date(s).toLocaleString('en-US', { timeZoneName: 'short' });
  } catch {
    return String(iso);
  }
}

function formatAmount(raw?: number | string | null): string {
  if (raw == null) return '-';
  return Number(raw).toFixed(4);
}

function isSuccess(status?: string | null): boolean {
  return !!status?.toLowerCase().includes('success');
}

function isPending(status?: string | null): boolean {
  return !!status?.toLowerCase().includes('pending');
}

// Explorer base: can be set via Vite env `VITE_EXPLORER_URL` to point to the correct network explorer.
// NOTE (Production Architecture): Ensure explorer is configured per environment (testnet/mainnet) and validated.
const EXPLORER_BASE = (import.meta.env.VITE_EXPLORER_URL as string) || 'https://testnet.arcscan.app/tx/';

function openExplorer(txHash: string): void {
  try {
    const url = `${EXPLORER_BASE}${txHash}`;
    window.open(url, '_blank', 'noopener,noreferrer');
  } catch (e) {
    console.error('[IndexPage] Failed to open explorer', e);
  }
}

// ── Balance display ───────────────────────────────────────────────────────────
const balanceDisplay = computed(() => store.bondBalance.toFixed(2));

// ── Sparkline ─────────────────────────────────────────────────────────────────
interface SparkPoint { x: number; y: number }

const sparkPoints = computed((): SparkPoint[] => {
  const vals = store.transactions
    .slice(0, 15)
    .map((t: TradingTransactionDto) => Number(t.amount) || 0)
    .reverse();
  if (vals.length < 2) return [];
  const max = Math.max(...vals);
  const min = Math.min(...vals);
  const range = max - min || 1;
  return vals.map((v, i) => ({
    x: (i / (vals.length - 1)) * 100,
    y: 28 - ((v - min) / range) * 24,
  }));
});

const sparkLine = computed((): string =>
  sparkPoints.value.map((p, i) => `${i === 0 ? 'M' : 'L'}${p.x} ${p.y}`).join(' '),
);

const sparkFill = computed((): string => {
  if (!sparkPoints.value.length) return '';
  const first = sparkPoints.value[0]!;
  const last = sparkPoints.value[sparkPoints.value.length - 1]!;
  return `${sparkLine.value} L${last.x} 28 L${first.x} 28 Z`;
});

// ── New-tx highlight ──────────────────────────────────────────────────────────
const recentIds = ref(new Set<string>());

watch(
  () => store.transactions,
  (next: TradingTransactionDto[], prev: TradingTransactionDto[]) => {
    const prevIds = new Set(prev.map((t) => t.id));
    next.forEach((t) => {
      if (!t.id || prevIds.has(t.id)) return;
      recentIds.value.add(t.id);
      setTimeout(() => recentIds.value.delete(t.id!), 3500);
    });
  },
  { deep: true },
);

// ── Init ──────────────────────────────────────────────────────────────────────
async function refresh(): Promise<void> {
  refreshing.value = true;
  await Promise.all([store.fetchAgent(), store.fetchTransactions()]);
  refreshing.value = false;
}

onMounted(async () => {
  try {
    await refresh();
  } catch (e) {
    console.error('[IndexPage] refresh failed', e);
  }
});
</script>

<style scoped>
.card-glow {
  box-shadow: 0 0 20px rgba(var(--q-primary-rgb, 99, 102, 241), 0.15);
}

.activity-table {
  max-height: 520px;
}

.activity-table :deep(thead tr th) {
  font-size: 11px;
  letter-spacing: 1.5px;
  color: var(--q-primary);
  background: transparent;
  position: sticky;
  top: 0;
  z-index: 1;
}

.text-mono {
  font-family: 'Courier New', Courier, monospace;
  font-size: 13px;
}

.row-highlight {
  animation: flashRow 3.5s ease-out forwards;
}

@keyframes flashRow {
  0% {
    background-color: rgba(99, 102, 241, 0.25);
  }

  100% {
    background-color: transparent;
  }
}
</style>
