/**
 * The line icons of the design. They are decorative: the button or link next to them
 * carries the text.
 */
const common = {
  viewBox: '0 0 24 24',
  fill: 'none',
  stroke: 'currentColor',
  strokeLinecap: 'round' as const,
  strokeLinejoin: 'round' as const,
  'aria-hidden': true,
};

export function MenuIcon({ className }: { className?: string }) {
  return (
    <svg {...common} strokeWidth={2} className={className}>
      <path d="M4 7h16M4 12h16M4 17h16" />
    </svg>
  );
}

export function CloseIcon({ className }: { className?: string }) {
  return (
    <svg {...common} strokeWidth={2} className={className}>
      <path d="M6 6l12 12M18 6L6 18" />
    </svg>
  );
}

export function ArrowRightIcon({ className }: { className?: string }) {
  return (
    <svg {...common} strokeWidth={2.4} className={className}>
      <path d="M5 12h14M13 6l6 6-6 6" />
    </svg>
  );
}

export function ArrowLeftIcon() {
  return (
    <svg {...common} strokeWidth={2.2}>
      <path d="M15 6l-6 6 6 6" />
    </svg>
  );
}

export function ChatIcon() {
  return (
    <svg {...common} strokeWidth={1.8}>
      <path d="M21 12a8 8 0 0 1-11.6 7.1L4 20.5l1.4-4.9A8 8 0 1 1 21 12z" />
    </svg>
  );
}

export function VideoIcon() {
  return (
    <svg {...common} strokeWidth={1.8}>
      <rect x="3" y="6" width="18" height="12" rx="3" />
      <path d="M10 9.5l5 2.5-5 2.5z" />
    </svg>
  );
}

export function FeedIcon() {
  return (
    <svg {...common} strokeWidth={1.8}>
      <path d="M5 5a14 14 0 0 1 14 14M5 11a8 8 0 0 1 8 8" />
      <circle cx="6" cy="18" r="1.5" />
    </svg>
  );
}

export function MailIcon() {
  return (
    <svg {...common} strokeWidth={1.8}>
      <rect x="3" y="5" width="18" height="14" rx="2.5" />
      <path d="M3.5 6.5l8.5 6.5 8.5-6.5" />
    </svg>
  );
}
