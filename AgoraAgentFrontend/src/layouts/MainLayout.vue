<template>
  <q-layout view="lHh Lpr lFf">
    <q-header elevated class="bg-dark text-white">
      <q-toolbar>
        <q-btn flat dense round icon="menu" aria-label="Menu" @click="toggleLeftDrawer" />

        <q-toolbar-title>AGORA AGENT</q-toolbar-title>

        <q-space />

        <q-icon
          :name="isConnected ? 'lens' : 'lens'"
          :color="isConnected ? 'positive' : 'grey-6'"
          size="12px"
        />
        <q-btn
          outline
          color="secondary"
          class="q-ml-sm"
          label="SIMULER TRADE ARC"
          @click="simulateTrade"
        />
      </q-toolbar>
    </q-header>

    <q-drawer v-model="leftDrawerOpen" show-if-above bordered>
      <q-list padding>
        <q-item clickable v-for="item in nav" :key="item.label" :to="item.to">
          <q-item-section avatar>
            <q-icon :name="item.icon" />
          </q-item-section>
          <q-item-section>{{ item.label }}</q-item-section>
        </q-item>
      </q-list>
    </q-drawer>

    <q-page-container>
      <router-view />
    </q-page-container>
  </q-layout>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import { useQuasar } from 'quasar';
import axios from 'axios';
import { HubConnectionState } from '@microsoft/signalr';
import { useAgentStore } from '../stores/agentStore';

const leftDrawerOpen = ref(true);
function toggleLeftDrawer() {
  leftDrawerOpen.value = !leftDrawerOpen.value;
}

const nav = [
  { label: 'Dashboard', to: '/', icon: 'dashboard' },
  { label: 'Mon Portefeuille', to: '/wallet', icon: 'account_balance_wallet' },
  { label: 'Stratégies', to: '/strategies', icon: 'insights' },
  { label: 'Rapports', to: '/reports', icon: 'assessment' },
];

const store = useAgentStore();
const $q = useQuasar();

const isConnected = computed(() => {
  const conn = store.connection;
  return !!conn && conn.state === HubConnectionState.Connected;
});

const simulateTrade = async () => {
  try {
    const base = (import.meta.env.VITE_API_URL as string) || 'http://localhost:5000';
    await axios.post(`${base}/api/demo/trigger-trade`);
  } catch {
    // ignore
  }
};

onMounted(() => {
  $q.dark.set(true);
  void store.connectSignalR();
});
</script>
