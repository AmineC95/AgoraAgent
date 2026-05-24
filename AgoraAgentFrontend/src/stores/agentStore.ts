import { defineStore } from 'pinia';
import axios from 'axios';
import * as signalR from '@microsoft/signalr';
import type { components } from '../api/models';

export type AgentDto = components['schemas']['AgentDto'];
export type TradingTransactionDto = components['schemas']['TradingTransactionDto'];

const API_BASE = (): string =>
  ((import.meta.env.VITE_API_URL as string) || 'http://localhost:5000').replace(/\/$/, '');

interface AgentState {
  agent: AgentDto | null;
  currentAgent: AgentDto | null;
  transactions: TradingTransactionDto[];
  connection: signalR.HubConnection | null;
  loading: boolean;
  connectionStatus: 'disconnected' | 'connecting' | 'connected' | 'error';
}

export const useAgentStore = defineStore('agent', {
  /**
   * Pinia store managing the agent state and live transactions.
   *
   * Responsibilities:
   * - Hold the currently selected agent and a list of recent trading transactions.
   * - Manage a SignalR HubConnection for real-time updates (`TradeUpdated`, `BalanceUpdated`).
   * - Provide actions to fetch data from the backend APIs and to execute demo trades.
   *
   * Notes:
   * - The store uses optimistic UI updates driven by server broadcasts.
  * - NOTE (Production Architecture): Do not persist or transmit raw API keys (BYOK) from the browser in cleartext; use a secure vault or ephemeral server-signed tokens.
   */
  state: (): AgentState => ({
    agent: null,
    currentAgent: null,
    transactions: [],
    connection: null,
    loading: false,
    connectionStatus: 'disconnected',
  }),

  getters: {
    bondBalance: (state): number => {
      const raw = state.currentAgent?.bondBalance ?? state.agent?.bondBalance;
      return raw !== undefined && raw !== null ? Number(raw) : 0;
    },
    isConnected: (state): boolean => state.connectionStatus === 'connected',
  },

  actions: {
    // ─── Internal helpers ───────────────────────────────────────────────────────
    _upsertTransaction(tx: TradingTransactionDto): void {
      if (!tx?.id) return;
      const idx = this.transactions.findIndex((t) => t.id === tx.id);
      if (idx >= 0) this.transactions[idx] = { ...this.transactions[idx], ...tx };
      else this.transactions.unshift(tx);
    },

    _updateAgentBalance(agentId: string, balance: number | string): void {
      // Update both currentAgent and agent if they match
      const numeric = Number(balance);
      if (this.currentAgent?.id === agentId) {
        this.currentAgent = { ...this.currentAgent, bondBalance: numeric };
      }
      if (this.agent?.id === agentId) {
        this.agent = { ...this.agent, bondBalance: numeric };
      }
    },

    // ─── API calls ──────────────────────────────────────────────────────────────
    async fetchAgent(agentId?: string): Promise<void> {
      try {
        const url = agentId
          ? `${API_BASE()}/api/analytics/status?agentId=${agentId}`
          : `${API_BASE()}/api/analytics/status`;
        const { data } = await axios.get<AgentDto | AgentDto[]>(url);

        // API may return a list when no agentId is provided — pick the first element
        if (Array.isArray(data)) {
          const first = data[0] ?? null;
          this.currentAgent = first;
          this.agent = first;
        } else {
          this.currentAgent = data;
          this.agent = data;
        }
      } catch (err) {
        console.error('[AgentStore] fetchAgent error', err);
      }
    },

    async fetchTransactions(agentId?: string): Promise<void> {
      try {
        const url = agentId
          ? `${API_BASE()}/api/analytics/performance?agentId=${agentId}`
          : `${API_BASE()}/api/analytics/performance`;
        const { data } = await axios.get<TradingTransactionDto[]>(url);
        this.transactions = data ?? [];
      } catch (err) {
        console.error('[AgentStore] fetchTransactions error', err);
      }
    },

    /**
     * Execute a demo trade via the backend demo endpoint.
     *
     * Security note: this action transmits the provided `apiKey` to the backend. For production,
     * do not send raw API keys from the browser; instead use a secure delegated flow or server-side proxy.
     *
     * @param payload - { apiKey, tradeAmount, customPrompt }
     * @returns the created TradingTransactionDto or null on failure
     */
    async executeTrade(payload: {
      apiKey: string;
      tradeAmount: number;
      customPrompt: string;
    }): Promise<TradingTransactionDto | null> {
      this.loading = true;
      try {
        const { data } = await axios.post<TradingTransactionDto>(
          `${API_BASE()}/api/demo/trigger-trade`,
          payload,
        );
        // Refresh agent so balance updates immediately (prefer currentAgent id)
        const idToFetch = this.currentAgent?.id ?? this.agent?.id;
        await this.fetchAgent(idToFetch);
        await this.fetchTransactions(idToFetch);
        return data ?? null;
      } catch (err) {
        console.error('[AgentStore] executeTrade error', err);
        return null;
      } finally {
        this.loading = false;
      }
    },

    // ─── SignalR ────────────────────────────────────────────────────────────────
    async connectSignalR(): Promise<void> {
      if (this.connectionStatus === 'connected') return;

      this.connectionStatus = 'connecting';
      const hubUrl = `${API_BASE()}/hubs/trade`;

      const connection = new signalR.HubConnectionBuilder()
        .withUrl(hubUrl)
        .withAutomaticReconnect()
        .configureLogging(signalR.LogLevel.Warning)
        .build();

      connection.on('TradeUpdated', (tx: TradingTransactionDto) => {
        this._upsertTransaction(tx);
      });

      connection.on('BalanceUpdated', (payload: Record<string, unknown>) => {
        const agentId = (payload['AgentId'] ?? payload['agentId']) as string | undefined;
        const balance = payload['BondBalance'] ?? payload['bondBalance'];
        if (agentId && balance !== undefined) {
          this._updateAgentBalance(agentId, balance as number | string);
        }
      });

      connection.onreconnecting(() => {
        this.connectionStatus = 'connecting';
      });
      connection.onreconnected(() => {
        this.connectionStatus = 'connected';
      });
      connection.onclose(() => {
        this.connectionStatus = 'disconnected';
      });

      try {
        await connection.start();
        this.connection = connection;
        this.connectionStatus = 'connected';
        console.info('[AgentStore] SignalR connected to', hubUrl);
      } catch (err) {
        console.error('[AgentStore] SignalR connection failed', err);
        this.connectionStatus = 'error';
      }
    },
  },
});
