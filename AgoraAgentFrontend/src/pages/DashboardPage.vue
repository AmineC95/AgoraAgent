<template>
  <q-page class="q-pa-md">
    <div class="row q-col-gutter-md">
      <div class="col-12 col-md-4">
        <q-card dark>
          <q-card-section>
            <div class="text-h6 text-weight-bold">FONDS DISPONIBLES</div>
            <div class="text-subtitle2 q-mt-xs">BOND BALANCE</div>

            <div class="row items-center q-mt-md">
              <div class="text-h4 text-weight-bold">{{ formattedBalance }}</div>
              <div class="q-ml-sm">USDC</div>
            </div>

            <svg viewBox="0 0 100 30" preserveAspectRatio="none" v-if="sparkPath">
              <path
                :d="sparkPath"
                stroke="var(--q-primary)"
                stroke-width="1.6"
                fill="none"
                stroke-linecap="round"
                stroke-linejoin="round"
              />
            </svg>
          </q-card-section>
        </q-card>
      </div>

      <div class="col-12 col-md-8">
        <q-card dark flat bordered>
          <q-card-section class="row items-center justify-between">
            <div class="text-h6 text-weight-bold">
              FLUX DE TRANSACTIONS TEMPS RÉEL (ARC NETWORK)
            </div>
          </q-card-section>

          <q-separator />

          <q-card-section>
            <q-table
              :rows="transactions"
              :columns="columns"
              row-key="id"
              dense
              flat
              dark
              :row-class="rowClass"
            >
              <template v-slot:body-cell-createdAt="props">
                <div class="mono">{{ formatTime(props.row.createdAt ?? '') }}</div>
              </template>

              <template v-slot:body-cell-action="props">
                <div class="row items-center">
                  <q-icon
                    :name="
                      (props.row.action ?? 'Buy') === 'Buy' ? 'arrow_upward' : 'arrow_downward'
                    "
                    :color="(props.row.action ?? 'Buy') === 'Buy' ? 'accent' : 'negative'"
                  />
                  <span class="q-ml-xs">{{ props.row.action ?? '' }}</span>
                </div>
              </template>

              <template v-slot:body-cell-status="props">
                <div
                  :class="[
                    (props.row.status ?? '').toLowerCase().includes('success')
                      ? 'text-positive'
                      : (props.row.status ?? '').toLowerCase().includes('pending')
                        ? 'text-warning'
                        : '',
                  ]"
                >
                  {{ props.row.status }}
                </div>
              </template>

              <template v-slot:body-cell-arcLink="props">
                <q-btn flat round dense icon="link" @click="openArcLink(props.row.txHash ?? '')" />
              </template>
            </q-table>
          </q-card-section>
        </q-card>
      </div>
    </div>
  </q-page>
</template>

<script lang="ts">
import { defineComponent, computed, ref, watch } from 'vue';
import { useAgentStore } from '../stores/agentStore';
import type { components } from '../api/models';

type TradingTransactionDto = components['schemas']['TradingTransactionDto'];

export default defineComponent({
  setup() {
    const store = useAgentStore();

    const transactions = computed<TradingTransactionDto[]>(() => store.transactions ?? []);
    const displayBalance = computed(() => store.currentAgent?.bondBalance ?? 0);

    const columns = [
      { name: 'createdAt', label: 'TIME', field: 'createdAt', sortable: true },
      { name: 'action', label: 'ACTION', field: 'action' },
      { name: 'amount', label: 'AMOUNT', field: 'amount' },
      { name: 'priceAtTrade', label: 'PRICE', field: 'priceAtTrade' },
      { name: 'status', label: 'STATUS', field: 'status' },
      { name: 'arcLink', label: 'ARC LINK', field: 'txHash' },
    ];

    const recentTxs = ref(new Set<string>());

    watch(
      transactions,
      (newArr: TradingTransactionDto[] = [], oldArr: TradingTransactionDto[] = []) => {
        const oldIds = new Set(oldArr.map((t) => t.id));
        newArr.forEach((t: TradingTransactionDto) => {
          const id = t.id;
          if (!id) return;
          if (!oldIds.has(id)) {
            recentTxs.value.add(id);
            setTimeout(() => recentTxs.value.delete(id), 3500);
          }
        });
      },
      { deep: true },
    );

    const rowClass = (row: TradingTransactionDto) => {
      const id = row.id;
      if (!id) return '';
      return recentTxs.value.has(id) ? 'bg-accent' : '';
    };

    const formatTime = (iso?: string | null) => {
      if (!iso) return '';
      try {
        return new Date(iso).toLocaleTimeString([], { hour12: false });
      } catch {
        return String(iso);
      }
    };

    const openArcLink = (txHash?: string) => {
      if (!txHash) return;
      const url = `https://testnet.arcscan.app/tx/${txHash}`;
      window.open(url, '_blank');
    };

    const formattedBalance = computed(() => `${Number(displayBalance.value ?? 0).toFixed(2)} USDC`);

    const sparkPath = computed(() => {
      const vals = (store.transactions ?? [])
        .slice(0, 12)
        .map((t: TradingTransactionDto) => Number(t.amount) || 0);
      if (vals.length === 0) return '';
      const max = Math.max(...vals);
      const min = Math.min(...vals);
      const pts = vals.map((v, i) => {
        const x = (i / Math.max(1, vals.length - 1)) * 100;
        const y = max === min ? 50 : 100 - ((v - min) / (max - min)) * 100;
        return { x, y };
      });
      return pts.map((p, i) => `${i === 0 ? 'M' : 'L'} ${p.x} ${p.y}`).join(' ');
    });

    return {
      transactions,
      columns,
      formattedBalance,
      rowClass,
      formatTime,
      openArcLink,
      sparkPath,
    };
  },
});
</script>
