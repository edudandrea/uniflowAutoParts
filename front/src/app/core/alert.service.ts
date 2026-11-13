import { Injectable } from '@angular/core';
import type { SweetAlertIcon } from 'sweetalert2';

@Injectable({ providedIn: 'root' })
export class AlertService {
  private async swal() { return (await import('sweetalert2/dist/sweetalert2.esm.all.js')).default; }
  private target(): HTMLElement { return document.querySelector<HTMLElement>('dialog[open]') ?? document.body; }
  notify(text: string, icon: SweetAlertIcon = 'info'): void {
    void this.swal().then(swal => swal.fire({ text, icon, target: this.target(),
      confirmButtonText: 'Entendi', confirmButtonColor: '#009d63', heightAuto: false,
    })).catch(() => { /* A mensagem também permanece visível na página. */ });
  }
  async confirm(text: string, title = 'Limpar venda?'): Promise<boolean> {
    try {
      const swal = await this.swal();
      return (await swal.fire({ title, text, icon: 'question', target: this.target(),
        showCancelButton: true, confirmButtonText: 'Sim, continuar', cancelButtonText: 'Voltar',
        confirmButtonColor: '#009d63', focusCancel: true, heightAuto: false,
      })).isConfirmed;
    } catch { return false; }
  }
}
