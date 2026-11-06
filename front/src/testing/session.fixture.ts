import { signal } from '@angular/core';
import { of } from 'rxjs';
import { HubSession } from '../app/core/session.service';

export const TEST_SESSION: HubSession = {
  authenticated: true, available: true, portalUrl: 'https://hub.example.test/?aplicacoes=1', csrfToken: 'csrf-test',
  empresa: { id: 1, nome: 'Empresa de teste', razaoSocial: 'Empresa de teste', cnpj: '11111111000111', hubTenantId: 'central-1' },
  usuario: { id: '1', sujeito: 'central-1', nome: 'Usuario de teste', email: 'teste@example.test', perfil: 'Operador' },
};
export function sessionStub() {
  return { session: signal(TEST_SESSION), empresaId: 1, load: () => of(TEST_SESSION),
    storageKey: (feature: string) => `uniflow:${feature}:empresa-1:usuario-central-1` };
}
