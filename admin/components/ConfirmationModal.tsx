import React from 'react';
import { AlertTriangle, X } from 'lucide-react';

export interface ConfirmationModalProps {
  isOpen: boolean;
  title: string;
  message: string;
  confirmText?: string;
  variant?: 'danger' | 'warning' | 'primary';
  confirmVariant?: 'danger' | 'warning' | 'primary';
  isLoading?: boolean;
  onConfirm: () => void | Promise<void>;
  onCancel?: () => void;
}

export function ConfirmationModal({
  isOpen,
  title,
  message,
  confirmText = 'Confirm',
  variant,
  confirmVariant = 'danger',
  isLoading = false,
  onConfirm,
  onCancel,
}: ConfirmationModalProps) {
  if (!isOpen) return null;

  const actualVariant = variant || confirmVariant;

  const variantStyles = {
    danger: 'bg-rose-600 hover:bg-rose-700 text-white',
    warning: 'bg-amber-600 hover:bg-amber-700 text-white',
    primary: 'bg-indigo-600 hover:bg-indigo-700 text-white',
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/80 backdrop-blur-sm animate-fadeIn">
      <div className="bg-slate-900 rounded-2xl shadow-2xl border border-slate-800 w-full max-w-md overflow-hidden">
        <div className="p-6">
          <div className="flex items-center justify-between pb-4 border-b border-slate-800">
            <div className="flex items-center space-x-3">
              <div className="p-2 bg-amber-500/10 text-amber-400 rounded-lg">
                <AlertTriangle className="w-5 h-5" />
              </div>
              <h3 className="text-base font-bold text-white">{title}</h3>
            </div>
            {onCancel && (
              <button onClick={onCancel} className="text-slate-400 hover:text-white p-1 transition-colors">
                <X className="w-5 h-5" />
              </button>
            )}
          </div>

          <div className="py-4 text-sm text-slate-300 leading-relaxed">{message}</div>

          <div className="flex items-center justify-end space-x-3 pt-4 border-t border-slate-800">
            {onCancel && (
              <button
                type="button"
                onClick={onCancel}
                disabled={isLoading}
                className="px-4 py-2 text-xs font-medium text-slate-400 hover:text-white bg-slate-800 rounded-xl transition-colors"
              >
                Cancel
              </button>
            )}
            <button
              type="button"
              onClick={onConfirm}
              disabled={isLoading}
              className={`px-4 py-2 text-xs font-semibold rounded-xl transition-colors ${variantStyles[actualVariant]}`}
            >
              {isLoading ? 'Processing...' : confirmText}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}

export default ConfirmationModal;
