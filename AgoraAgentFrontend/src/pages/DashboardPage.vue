<template>
  <q-page class="q-pa-md">
    <q-toolbar>
        <q-toolbar-title>Agora Dashboard</q-toolbar-title>
      </q-toolbar>

      <div class="row q-col-gutter-md">
        <div class="col-12 col-md-4">
          <q-card class="bg-black text-white">
            <q-card-section class="text-subtitle2">Bond Balance</q-card-section>
            <q-card-section class="text-h3">{{ displayBalance }}</q-card-section>
          </q-card>
        </div>

        <div class="col-12 col-md-8">
          <q-card>
            <q-card-section>
              <div class="row items-center justify-between">
                <div class="text-h6">Transactions</div>
                <div>
                  <q-btn flat label="Connect" @click="connect" />
                </div>
              </div>
            </q-card-section>

            <q-table :rows="transactions" :columns="columns" row-key="id" flat dense>
              <template v-slot:body-cell-status="props">
                <q-badge :color="statusColor(props.value)" :label="props.value ?? 'Unknown'" />
              </template>
            </q-table>
          </q-card>
        </div>
      </div>
  </q-page>
</template>

<script lang="ts">
import { defineComponent, computed, onMounted } from 'vue';
import { useAgentStore } from '../stores/agentStore';

export default defineComponent({
  setup() {
    const store = useAgentStore();
    const transactions = computed(() => store.transactions ?? []);
    const displayBalance = computed(() => store.currentAgent?.bondBalance ?? 0);

    const columns = [
      { name: 'createdAt', label: 'Time', field: 'createdAt', sortable: true },
      { name: 'action', label: 'Action', field: 'action' },
      { name: 'amount', label: 'Amount', field: 'amount' },
      { name: 'priceAtTrade', label: 'Price', field: 'priceAtTrade' },
      { name: 'status', label: 'Status', field: 'status' },
    ];

    const statusColor = (status: string | undefined) => {
      if (!status) return 'grey';
      const s = status.toLowerCase();
      if (s.includes('success')) return 'positive';
      if (s.includes('failed') || s.includes('fail')) return 'negative';
      return 'warning';
    };

    const connect = async () => {
      await store.connectSignalR();
    };

    onMounted(() => {
      void connect();
    });

    return { transactions, columns, displayBalance, connect, statusColor };
  },
});
</script>
