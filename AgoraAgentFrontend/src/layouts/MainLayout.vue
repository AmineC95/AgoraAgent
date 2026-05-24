<template>
  <q-layout view="lHh Lpr lFf">
    <q-header elevated class="bg-dark text-white">
      <q-toolbar class="q-px-md">
        <!-- Brand -->
        <div class="column q-mr-lg">
          <div
            class="text-h6 text-weight-bold text-primary"
            style="letter-spacing: 2px; line-height: 1"
          >
            AGORA AGENT
          </div>
          <div class="text-caption text-grey-5" style="font-size: 10px; letter-spacing: 1px">
            AUTONOMOUS AI TRADING · ARC TESTNET
          </div>
        </div>

        <q-space />

        <!-- Inputs group -->
        <div class="row items-center q-gutter-sm">
          <q-input
            dense
            outlined
            hide-bottom-space
            type="number"
            v-model.number="tradeAmount"
            label="Amount (USDC)"
            style="width: 130px"
            min="0.01"
            step="0.01"
          />

          <q-input
            dense
            outlined
            hide-bottom-space
            type="text"
            v-model="customPrompt"
            label="AI Strategy Prompt"
            placeholder="e.g., The market is crashing, be defensive"
            style="width: 380px"
          />

          <q-input
            dense
            outlined
            hide-bottom-space
            type="password"
            v-model="apiKey"
            label="BYOK API Key"
            placeholder="gsk_..."
            style="width: 260px"
          />
        </div>

        <!-- Connection indicator -->
        <q-icon
          name="circle"
          :color="store.isConnected ? 'positive' : 'grey-6'"
          size="10px"
          class="q-mx-sm cursor-pointer"
        >
          <q-tooltip>
            {{ store.isConnected ? 'Live feed connected' : 'Connecting to live feed...' }}
          </q-tooltip>
        </q-icon>

        <!-- CTA button -->
        <q-btn
          unelevated
          color="primary"
          icon="bolt"
          label="EXECUTE AI TRADE"
          :loading="store.loading"
          :disable="store.loading"
          class="q-ml-xs text-weight-bold"
          style="letter-spacing: 1px"
          @click="onExecuteTrade"
        />
      </q-toolbar>
    </q-header>

    <q-page-container>
      <router-view />
    </q-page-container>
  </q-layout>
</template>

<script setup lang="ts">
import { ref, watch, onMounted } from 'vue';
import { useQuasar, Notify } from 'quasar';
import { useAgentStore } from '../stores/agentStore';

const $q = useQuasar();
const store = useAgentStore();

// Persisted API key (BYOK)
// NOTE (Production Architecture): Persisting BYOK API keys in localStorage is insecure. Replace with
// a secure vault, short-lived tokens, or server-side delegation before shipping to prod.
const apiKey = ref<string>(localStorage.getItem('apiKey') ?? '');
watch(apiKey, (v) => {
  try {
    if (v) {
      localStorage.setItem('apiKey', v);
    } else {
      localStorage.removeItem('apiKey');
    }
  } catch (e) {
    console.error('[MainLayout] localStorage access failed', e);
  }
});

const tradeAmount = ref<number>(0.01);
const customPrompt = ref<string>('');

// Execute trade
async function onExecuteTrade(): Promise<void> {
  const result = await store.executeTrade({
    apiKey: apiKey.value,
    tradeAmount: tradeAmount.value,
    customPrompt: customPrompt.value,
  });

  if (result) {
    Notify.create({
      type: 'positive',
      icon: 'check_circle',
      message: `Trade dispatched - ${result.action ?? 'BUY'} ${Number(result.amount ?? 0).toFixed(4)} USDC`,
      timeout: 4000,
    });
  } else {
    Notify.create({
      type: 'warning',
      icon: 'warning',
      message: 'Trade sent (fallback mode - check RPC / private key).',
      timeout: 4000,
    });
  }
}

// Init
onMounted(() => {
  $q.dark.set(true);
  store.connectSignalR().catch((err) => console.error('[MainLayout] SignalR connect failed', err));
});
</script>
