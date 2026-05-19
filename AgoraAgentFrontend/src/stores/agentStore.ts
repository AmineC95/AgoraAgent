import { defineStore } from 'pinia';
import axios from 'axios';
import * as signalR from '@microsoft/signalr';
import type { components } from '../api/models';

type AgentDto = components['schemas']['AgentDto'];
type TradingTransactionDto = components['schemas']['TradingTransactionDto'];

type BalanceUpdatedPayload = {
  AgentId?: string;
  agentId?: string;
  BondBalance?: number | string;
  bondBalance?: number | string;
} & Record<string, unknown>;

export const useAgentStore = defineStore('agent', {
  state: () => ({
    currentAgent: null as AgentDto | null,
    agents: [] as AgentDto[],
    transactions: [] as TradingTransactionDto[],
    connection: null as signalR.HubConnection | null,
  }),
  actions: {
    setAgents(a: AgentDto[]) {
      this.agents = a;
    },
    setCurrentAgent(agent: AgentDto) {
      this.currentAgent = agent;
    },
    updateAgent(agent: AgentDto) {
      const idx = this.agents.findIndex((x) => x.id === agent.id);
      if (idx >= 0) this.agents[idx] = agent;
      else this.agents.push(agent);
      if (!this.currentAgent || this.currentAgent.id === agent.id) this.currentAgent = agent;
    },
    addOrUpdateTransaction(tx: TradingTransactionDto) {
      if (!tx || !tx.id) return;
      const idx = this.transactions.findIndex((t) => t.id === tx.id);
      if (idx >= 0) this.transactions[idx] = { ...this.transactions[idx], ...tx };
      else this.transactions.unshift(tx);
    },
    async connectSignalR() {
      if (this.connection) {
        try {
          await this.connection.start();
          return this.connection;
        } catch {
          // ignore
        }
      }

      const base = (import.meta.env.VITE_API_URL as string) || 'http://localhost:5000';
      const hubUrl = `${base.replace(/\/$/, '')}/hubs/trade`;
      const connection = new signalR.HubConnectionBuilder()
        .withUrl(hubUrl)
        .withAutomaticReconnect()
        .build();

      connection.on('TradeUpdated', (payload: TradingTransactionDto) => {
        this.addOrUpdateTransaction(payload);
      });
      connection.on('BalanceUpdated', (payload: BalanceUpdatedPayload) => {
        try {
          const agentId = payload?.AgentId ?? payload?.agentId;
          const balance = payload?.BondBalance ?? payload?.bondBalance;
          if (!agentId) return;
          if (balance === undefined) return;
          // Update agents list
          const idx = this.agents.findIndex((a) => a.id === agentId);
          if (idx >= 0) {
            this.agents[idx] = { ...this.agents[idx], bondBalance: balance };
          }
          // Update current agent if matching
          if (this.currentAgent && this.currentAgent.id === agentId) {
            this.currentAgent = { ...this.currentAgent, bondBalance: balance };
          }
        } catch (error) {
          console.error('BalanceUpdated handler error', error);
        }
      });

      try {
        await connection.start();
        this.connection = connection;
        console.info('SignalR connected to', hubUrl);
      } catch (error) {
        console.error('SignalR connection error', error);
      }

      return connection;
    },
    async fetchInitial(agentId?: string) {
      try {
        const base = (import.meta.env.VITE_API_URL as string) || 'http://localhost:5000';
        if (agentId) {
          const resp = await axios.get(`${base}/api/analytics/status?agentId=${agentId}`);
          const agent = resp.data as AgentDto;
          this.setCurrentAgent(agent);
        }
        const perf = await axios.get(`${base}/api/analytics/performance`);
        this.transactions = perf.data as TradingTransactionDto[];
      } catch (error) {
        console.error('Error fetching initial data', error);
      }
    },
  },
});
