import { useEffect, useMemo, useRef, useState } from 'react';
import { batchApi, furnaceApi } from '../services/api';
import { realtimeClient } from '../services/signalr';
import type { BatchDto, FurnaceDto, FurnaceClearedDto, FurnaceUpdateDto, CalculationStepDto } from '../types';

/**
 * Universal Smart TV background video player.
 * Automatically resolves Smart TV (Tizen / webOS / Android TV) autoplay restrictions
 * by explicitly forcing DOM properties (muted, defaultMuted) and adding event fallbacks.
 */
function TvBackgroundVideo({ src, opacity = 0.65 }: { src: string; opacity?: number }) {
  const videoRef = useRef<HTMLVideoElement>(null);

  useEffect(() => {
    const video = videoRef.current;
    if (!video) return;

    // Smart TV Fix: Explicitly enforce DOM properties on the underlying HTML5 media element
    video.muted = true;
    video.defaultMuted = true;

    const playVideo = () => {
      if (video) {
        video.muted = true;
        const playPromise = video.play();
        if (playPromise !== undefined) {
          playPromise.catch((err) => {
            console.warn('Smart TV autoplay held, awaiting user/remote signal', err);
          });
        }
      }
    };

    playVideo();

    // In case Smart TV OS holds playback until first remote keypress or screen touch:
    const handleRemoteActivity = () => {
      playVideo();
    };

    window.addEventListener('keydown', handleRemoteActivity, { passive: true });
    window.addEventListener('click', handleRemoteActivity, { passive: true });

    return () => {
      window.removeEventListener('keydown', handleRemoteActivity);
      window.removeEventListener('click', handleRemoteActivity);
    };
  }, [src]);

  return (
    <video
      ref={videoRef}
      src={src}
      autoPlay
      loop
      muted
      // @ts-ignore
      defaultMuted
      playsInline
      preload="auto"
      // @ts-ignore
      webkit-playsinline="true"
      style={{
        position: 'absolute',
        top: 0,
        left: 0,
        width: '100%',
        height: '100%',
        objectFit: 'cover',
        opacity,
        zIndex: 0,
        pointerEvents: 'none'
      }}
      onLoadedMetadata={(e) => {
        const v = e.currentTarget;
        v.muted = true;
        v.play().catch(() => {});
      }}
      onCanPlay={(e) => {
        const v = e.currentTarget;
        if (v.paused) {
          v.muted = true;
          v.play().catch(() => {});
        }
      }}
    >
      <source src={src} type="video/mp4" />
    </video>
  );
}

interface CustomParamMeta {
  id: string;
  name: string;
  value: string;
  unit: string;
}

interface BatchMetadata {
  userRemarks: string;
  weightText: string;
  units: { si?: string; ti?: string; mg?: string; sr?: string };
  params: CustomParamMeta[];
  calculationDetails?: CalculationStepDto[];
}

const statusMode = (indicator?: number): 'green' | 'yellow' | 'red' | 'none' => {
  if (indicator === 1) return 'green';
  if (indicator === 2) return 'yellow';
  if (indicator === 3) return 'red';
  return 'none';
};

interface OperationalStatusConfig {
  key: string;
  cardClass: string;
  badgeClass: string;
  badgeStyle?: React.CSSProperties;
  badgeText: string;
  icon: string;
  title: string;
  instruction: string;
  color: string;
}

const getOperationalStatusConfig = (status?: string): OperationalStatusConfig => {
  const norm = (status || '').toLowerCase().trim();
  switch (norm) {
    case 'empty':
      return {
        key: 'empty',
        cardClass: 'status-empty',
        badgeClass: 'bg-info text-dark',
        badgeText: 'EMPTY',
        icon: 'bi-circle',
        title: 'Furnace is Empty',
        instruction: 'Status: Empty (Ready for charge)',
        color: '#06b6d4',
      };
    case 'melting':
    case 'inmelting':
    case 'in melting':
      return {
        key: 'melting',
        cardClass: 'status-melting',
        badgeClass: 'bg-warning text-dark',
        badgeText: 'IN MELTING',
        icon: 'bi-fire',
        title: 'In Melting Status',
        instruction: 'Status: In Melting (Heat on)',
        color: '#f97316',
      };
    case 'maintenance':
    case 'maintainance':
    case 'maintane':
      return {
        key: 'maintenance',
        cardClass: 'status-maintenance',
        badgeClass: 'text-white',
        badgeStyle: { backgroundColor: '#8b5cf6' },
        badgeText: 'MAINTENANCE',
        icon: 'bi-tools',
        title: 'Under Maintenance',
        instruction: 'Status: Maintenance',
        color: '#8b5cf6',
      };
    case 'breakdown':
      return {
        key: 'breakdown',
        cardClass: 'status-breakdown',
        badgeClass: 'bg-warning text-dark',
        badgeText: 'BREAKDOWN',
        icon: 'bi-exclamation-triangle-fill',
        title: 'Furnace Breakdown',
        instruction: 'Status: Breakdown (Action needed)',
        color: '#eab308',
      };
    case 'emergency':
      return {
        key: 'emergency',
        cardClass: 'status-emergency',
        badgeClass: 'bg-danger text-white',
        badgeText: 'EMERGENCY',
        icon: 'bi-sign-stop-fill',
        title: 'Emergency State',
        instruction: 'Status: EMERGENCY ALERT',
        color: '#dc2626',
      };
    case 'waitingforbatch':
    case 'waiting for batch':
    case 'waiting':
    case 'idle':
    default:
      return {
        key: 'waiting',
        cardClass: 'status-waiting',
        badgeClass: 'bg-secondary text-white',
        badgeText: 'NO BATCH',
        icon: 'bi-clock-history',
        title: 'Waiting for batch',
        instruction: 'Instruct: —',
        color: '#64748b',
      };
  }
};

const formatFurnaceNumber = (num?: string): string => {
  if (!num) return '—';
  return num.replace(/^F/i, '');
};

const formatReading = (value: number, digits = 4): string => {
  if (Number.isInteger(value)) return value.toString();
  return Number(value).toLocaleString(undefined, { maximumFractionDigits: digits });
};

const formatWeightValue = (value: number) => {
  return formatReading(value, 2);
};

const formatWeightUnit = (value: number, originalUnit: string) => {
  const origLower = originalUnit.toLowerCase();
  if (origLower === 'g' || origLower === 'grams' || origLower === 'gm' || origLower === 'gms') {
    return 'grams';
  }
  return originalUnit;
};

const getColorSwatches = (colorCode?: string) => {
  const code = colorCode || 'Yellow-blue';
  const colorMap: Record<string, string> = {
    yellow: '#eab308',
    blue: '#2563eb',
    green: '#16a34a',
    red: '#dc2626',
    black: '#18181b',
    brown: '#78350f',
    pink: '#ec4899',
  };

  const normalizedCode = code.replace(/[-&,]/g, ' ').replace(/\band\b/gi, ' ').replace(/\s+/g, ' ').trim();
  const parts = normalizedCode.split(' ');
  const validParts = parts.filter(p => colorMap[p.toLowerCase()]);
  
  if (validParts.length > 0) {
    if (validParts.length === 2 && validParts[0].toLowerCase() === 'black' && validParts[1].toLowerCase() === 'green') {
      return [colorMap['green'], colorMap['black']];
    }
    return validParts.map((p) => colorMap[p.toLowerCase()]);
  }
  
  return ['#00c9a7']; // fallback
};

const parseBatchMetadata = (remarksStr?: string): BatchMetadata => {
  if (!remarksStr) return { userRemarks: '', weightText: '', units: {}, params: [], calculationDetails: [] };
  try {
    if (remarksStr.startsWith('{') && remarksStr.endsWith('}')) {
      const parsed = JSON.parse(remarksStr) as {
        userRemarks?: string;
        weightText?: string;
        units?: { si?: string; ti?: string; mg?: string; sr?: string };
        params?: CustomParamMeta[];
        calculationDetails?: CalculationStepDto[];
      };
      return {
        userRemarks: parsed.userRemarks || '',
        weightText: parsed.weightText || '',
        units: parsed.units || {},
        params: Array.isArray(parsed.params) ? parsed.params : [],
        calculationDetails: Array.isArray(parsed.calculationDetails) ? parsed.calculationDetails : [],
      };
    }
  } catch {
    // plain text remarks
  }
  return { userRemarks: remarksStr, weightText: '', units: {}, params: [], calculationDetails: [] };
};

const getChemicalCardStyle = (symbol: string, index: number) => {
  const sym = symbol.toUpperCase();
  if (sym.includes('TI')) {
    return {
      color: '#00E5FF',
      border: '#00E5FF',
      bgSubtle: 'rgba(0, 229, 255, 0.15)',
      glow: 'rgba(0, 229, 255, 0.4)',
    };
  }
  if (sym.includes('MG')) {
    return {
      color: '#10B981',
      border: '#10B981',
      bgSubtle: 'rgba(16, 185, 129, 0.15)',
      glow: 'rgba(16, 185, 129, 0.4)',
    };
  }
  if (sym.includes('SR')) {
    return {
      color: '#E066FF',
      border: '#E066FF',
      bgSubtle: 'rgba(224, 102, 255, 0.15)',
      glow: 'rgba(224, 102, 255, 0.4)',
    };
  }
  if (sym.includes('SI')) {
    return {
      color: '#FACC15',
      border: '#FACC15',
      bgSubtle: 'rgba(250, 204, 21, 0.15)',
      glow: 'rgba(250, 204, 21, 0.4)',
    };
  }
  const palette = [
    { color: '#38BDF8', border: '#38BDF8', bgSubtle: 'rgba(56, 189, 248, 0.15)', glow: 'rgba(56, 189, 248, 0.4)' },
    { color: '#FB923C', border: '#FB923C', bgSubtle: 'rgba(251, 146, 60, 0.15)', glow: 'rgba(251, 146, 60, 0.4)' },
    { color: '#EC4899', border: '#EC4899', bgSubtle: 'rgba(236, 72, 153, 0.15)', glow: 'rgba(236, 72, 153, 0.4)' },
  ];
  return palette[index % palette.length];
};

export default function DisplayScreen() {
  const [furnaces, setFurnaces] = useState<FurnaceDto[]>([]);
  const [selectedView, setSelectedView] = useState<string>(() => {
    const urlParams = new URLSearchParams(window.location.search);
    const viewParam = urlParams.get('view');
    if (viewParam) return viewParam;
    return localStorage.getItem('displaySelectedView') || 'COMBO_ALL';
  });
  const [singleBatch, setSingleBatch] = useState<BatchDto | null>(null);
  const [comboBatches, setComboBatches] = useState<Record<string, BatchDto | null>>({});
  const [connected, setConnected] = useState(() => realtimeClient.isConnected());
  const [systemLive, setSystemLive] = useState(true);
  const [isFullscreen, setIsFullscreen] = useState(false);
  const [isDarkMode, setIsDarkMode] = useState(() => {
    return localStorage.getItem('displayTheme') === 'light' ? false : true;
  });

  const toggleTheme = () => {
    setIsDarkMode(prev => {
      const newTheme = !prev;
      localStorage.setItem('displayTheme', newTheme ? 'dark' : 'light');
      return newTheme;
    });
  };

  const handleNextView = () => {
    const options = [
      ...furnaces.map(f => f.number),
      'COMBO_1_7', 'COMBO_8_14', 'COMBO_15_20', 'COMBO_21_25', 'COMBO_ALL'
    ];
    const currentIndex = options.indexOf(selectedView);
    const nextIndex = (currentIndex + 1) % options.length;
    const nextView = options[nextIndex];
    setSelectedView(nextView);
    localStorage.setItem('displaySelectedView', nextView);
  };

  const handlePrevView = () => {
    const options = [
      ...furnaces.map(f => f.number),
      'COMBO_1_7', 'COMBO_8_14', 'COMBO_15_20', 'COMBO_21_25', 'COMBO_ALL'
    ];
    const currentIndex = options.indexOf(selectedView);
    const prevIndex = (currentIndex - 1 + options.length) % options.length;
    const prevView = options[prevIndex];
    setSelectedView(prevView);
    localStorage.setItem('displaySelectedView', prevView);
  };

  useEffect(() => {
    const handleFullscreenChange = () => {
      setIsFullscreen(!!document.fullscreenElement);
    };
    document.addEventListener('fullscreenchange', handleFullscreenChange);
    return () => document.removeEventListener('fullscreenchange', handleFullscreenChange);
  }, []);

  // Auto-fullscreen when switching to any Combo Display view
  useEffect(() => {
    if (selectedView.startsWith('COMBO_')) {
      const el = document.documentElement as any;
      const requestFs = el.requestFullscreen || el.webkitRequestFullscreen || el.mozRequestFullScreen || el.msRequestFullscreen;
      
      if (requestFs && !document.fullscreenElement) {
        try {
          requestFs.call(el).catch(() => {
            // Silently ignore if blocked
          });
        } catch (e) {
          // Ignore synchronous errors on very old browsers
        }
      }
    }
  }, [selectedView]);

  useEffect(() => {
    void realtimeClient.connect();
  }, []);

  useEffect(() => {
    const loadFurnaces = () => {
      void furnaceApi.getAll().then((list) => {
        const active = list.filter((f) => f.isActive);
        setFurnaces(active);
        if (!selectedView && active.length) setSelectedView(active[0].number);
      });
    };
    loadFurnaces();
    // Refresh furnace list every 30 seconds so newly activated/deactivated
    // furnaces appear on the display without requiring a manual page refresh.
    const interval = setInterval(loadFurnaces, 30000);
    return () => clearInterval(interval);
  }, []);

  const isCombo = selectedView.startsWith('COMBO_');

  const targetFurnaceNumbers = useMemo(() => {
    if (!isCombo) return [selectedView];
    if (selectedView === 'COMBO_1_7') return ['F1', 'F2', 'F3', 'F4', 'F5', 'F6', 'F7'];
    if (selectedView === 'COMBO_8_14') return ['F8', 'F9', 'F10', 'F11', 'F12', 'F13', 'F14'];
    if (selectedView === 'COMBO_15_20') return ['F15', 'F16', 'F17', 'F18', 'F19', 'F20'];
    if (selectedView === 'COMBO_21_25') return ['F21', 'F22', 'F23', 'F24', 'F25'];
    if (selectedView === 'COMBO_ALL') return Array.from({ length: 25 }, (_, i) => `F${i + 1}`);
    return [];
  }, [selectedView, isCombo]);

  // Load Batch Data for single or combo view
  useEffect(() => {
    if (furnaces.length === 0 || targetFurnaceNumbers.length === 0) return;

    if (!isCombo) {
      const furnace = furnaces.find((f) => f.number === selectedView || f.number.replace(/^F/i, '') === selectedView.replace(/^F/i, ''));
      if (furnace) {
        void batchApi.getActive(furnace.id).then(setSingleBatch).catch(() => setSingleBatch(null));
        void realtimeClient.joinFurnace(furnace.number);
      }
      return () => {
        if (furnace) void realtimeClient.leaveFurnace(furnace.number);
      };
    } else {
      // Combo Mode: Fetch active batch for each target furnace
      const initialMap: Record<string, BatchDto | null> = {};
      const loadPromises = targetFurnaceNumbers.map(async (num) => {
        const furnace = furnaces.find((f) => f.number === num || f.number.replace(/^F/i, '') === num.replace(/^F/i, ''));
        if (furnace) {
          try {
            const b = await batchApi.getActive(furnace.id);
            const canon = num.toUpperCase().startsWith('F') ? num.toUpperCase() : `F${num}`;
            const raw = canon.replace(/^F/i, '');
            initialMap[canon] = b;
            initialMap[raw] = b;
            initialMap[num] = b;
          } catch {
            initialMap[num] = null;
          }
          void realtimeClient.joinFurnace(furnace.number);
        } else {
          initialMap[num] = null;
        }
      });

      void Promise.all(loadPromises).then(() => {
        setComboBatches((prev) => ({ ...prev, ...initialMap }));
      });

      return () => {
        targetFurnaceNumbers.forEach((num) => {
          void realtimeClient.leaveFurnace(num);
        });
      };
    }
  }, [selectedView, furnaces, isCombo, targetFurnaceNumbers]);

  // Handle SignalR updates
  useEffect(() => {
    const unSubState = realtimeClient.onState(setConnected);
    const unSubSystemState = realtimeClient.onSystemState(setSystemLive);
    const unSubUpdate = realtimeClient.onUpdate((u: FurnaceUpdateDto) => {
      const fNum = u.furnaceNumber.toUpperCase();
      const canonicalFNum = fNum.startsWith('F') ? fNum : `F${fNum}`;
      const rawNum = canonicalFNum.replace(/^F/i, '');

      if (!isCombo) {
        if (canonicalFNum === selectedView.toUpperCase() || rawNum === selectedView.toUpperCase().replace(/^F/i, '')) {
          setSingleBatch((prev) =>
            prev
              ? {
                  ...prev,
                  batchNumber: u.batchNumber,
                  si: u.si,
                  ti: u.ti,
                  mg: u.mg,
                  sr: u.sr ?? u.futureChemical1,
                  colorCode: u.colorCode || prev.colorCode,
                  indicator: u.indicator,
                  instruction: u.instruction,
                  remarks: u.remarks,
                  startedAtUtc: u.updatedAtUtc,
                }
              : {
                  id: 0,
                  batchNumber: u.batchNumber,
                  furnaceId: 0,
                  furnaceNumber: u.furnaceNumber,
                  furnaceName: u.furnaceNumber,
                  operatorId: 0,
                  operatorName: '',
                  status: 1,
                  isActive: true,
                  startedAtUtc: u.updatedAtUtc,
                  indicator: u.indicator,
                  colorCode: u.colorCode || 'Yellow-blue',
                  instruction: u.instruction,
                  remarks: u.remarks,
                  si: u.si,
                  ti: u.ti,
                  mg: u.mg,
                  sr: u.sr ?? u.futureChemical1,
                  futureChemical1: u.futureChemical1,
                  futureChemical2: u.futureChemical2,
                  futureChemical3: u.futureChemical3,
                },
          );
        }
      } else {
        setComboBatches((prev) => {
          const existing = prev[canonicalFNum] || prev[rawNum] || prev[fNum];
          const updatedBatch: BatchDto = existing
            ? {
                ...existing,
                batchNumber: u.batchNumber,
                si: u.si,
                ti: u.ti,
                mg: u.mg,
                sr: u.sr ?? u.futureChemical1,
                colorCode: u.colorCode || existing.colorCode,
                indicator: u.indicator,
                instruction: u.instruction,
                remarks: u.remarks,
                startedAtUtc: u.updatedAtUtc,
              }
            : {
                id: 0,
                batchNumber: u.batchNumber,
                furnaceId: 0,
                furnaceNumber: u.furnaceNumber,
                furnaceName: u.furnaceNumber,
                operatorId: 0,
                operatorName: '',
                status: 1,
                isActive: true,
                startedAtUtc: u.updatedAtUtc,
                indicator: u.indicator,
                colorCode: u.colorCode || 'Yellow-blue',
                instruction: u.instruction,
                remarks: u.remarks,
                si: u.si,
                ti: u.ti,
                mg: u.mg,
                sr: u.sr ?? u.futureChemical1,
                futureChemical1: u.futureChemical1,
                futureChemical2: u.futureChemical2,
                futureChemical3: u.futureChemical3,
              };
          return {
            ...prev,
            [canonicalFNum]: updatedBatch,
            [rawNum]: updatedBatch,
            [fNum]: updatedBatch,
          };
        });
      }

      // Also update furnaces activeBatchNumber in memory so badges and views update instantly
      setFurnaces((prev) =>
        prev.map((f) => {
          const fn = f.number.toUpperCase();
          if (fn === canonicalFNum || fn.replace(/^F/i, '') === rawNum) {
            return {
              ...f,
              activeBatchNumber: u.batchNumber,
              operationalStatus: 'Melting',
              operationalRemarks: undefined,
            };
          }
          return f;
        })
      );
    });

    const unSubClear = realtimeClient.onClear((c: FurnaceClearedDto) => {
      const fNum = c.furnaceNumber.toUpperCase();
      const canonicalFNum = fNum.startsWith('F') ? fNum : `F${fNum}`;
      const rawNum = canonicalFNum.replace(/^F/i, '');

      if (!isCombo) {
        if (canonicalFNum === selectedView.toUpperCase() || rawNum === selectedView.toUpperCase().replace(/^F/i, '')) {
          setSingleBatch(null);
        }
      } else {
        setComboBatches((prev) => ({
          ...prev,
          [canonicalFNum]: null,
          [rawNum]: null,
          [fNum]: null,
        }));
      }

      setFurnaces((prev) =>
        prev.map((f) => {
          const fn = f.number.toUpperCase();
          if (fn === canonicalFNum || fn.replace(/^F/i, '') === rawNum) {
            return {
              ...f,
              activeBatchNumber: null,
              activeBatchId: null,
              operationalStatus: c.operationalStatus || 'Empty',
              operationalRemarks: 'Furnace Cleared',
              operationalStatusUpdatedAtUtc: c.clearedAtUtc,
            };
          }
          return f;
        })
      );
    });

    const unSubFurnaceStatus = realtimeClient.onFurnaceStatusChanged((statusData) => {
      setFurnaces((prev) =>
        prev.map((f) =>
          f.id === statusData.furnaceId || f.number.toUpperCase() === statusData.furnaceNumber.toUpperCase()
            ? {
                ...f,
                operationalStatus: statusData.operationalStatus,
                operationalRemarks: statusData.operationalRemarks,
                operationalStatusUpdatedAtUtc: statusData.updatedAtUtc,
              }
            : f
        )
      );
    });

    // Handle PDF Parsed → update display immediately when a valid PDF for the current
    // furnace is parsed — no page refresh needed.
    const unSubPdf = realtimeClient.onPdfParsed((data) => {
      // Safety guard: If the parsed PDF has any warnings (missing fields, mapping errors, lab rejection)
      // or missing core identifiers, DO NOT PASS THAT TO THE DISPLAY!
      if (data.warnings && data.warnings.length > 0) return;
      if (!data.partName || !data.gradeName || !data.batchNumber) return;

      let furnaceNum = data.furnaceNumber;
      if (!furnaceNum && data.batchNumber && data.batchNumber.length >= 3) {
        const charCode = data.batchNumber.toUpperCase().charCodeAt(2);
        if (charCode >= 65 && charCode <= 90) {
          furnaceNum = (charCode - 65) + 1;
        }
      }

      const pdfFurnaceNum = furnaceNum != null
        ? `F${furnaceNum}`
        : null;

      if (!pdfFurnaceNum) return;

      const normalizedPdf = pdfFurnaceNum.toUpperCase().replace(/^F0*/i, 'F');
      const rawNum = normalizedPdf.replace(/^F/i, '');

      // Auto-activate furnace in DisplayScreen state
      setFurnaces(prev => prev.map(f => {
        const fn = f.number.toUpperCase();
        if (fn === normalizedPdf || fn.replace(/^F/i, '') === rawNum) {
          return {
            ...f,
            isActive: true,
            activeBatchNumber: data.batchNumber,
            operationalStatus: 'Melting',
            operationalRemarks: undefined,
          };
        }
        return f;
      }));

      const matchesSingle = !isCombo && (
        selectedView.toUpperCase() === normalizedPdf ||
        selectedView.toUpperCase().replace(/^F0*/i, 'F') === normalizedPdf
      );
      const matchesCombo = isCombo && (
        targetFurnaceNumbers.some(n => n.toUpperCase() === normalizedPdf ||
          n.toUpperCase().replace(/^F0*/i, 'F') === normalizedPdf)
      );

      if (!matchesSingle && !matchesCombo) return;

      const furnaceEntry = furnaces.find(f =>
        f.number.toUpperCase() === normalizedPdf ||
        f.number.toUpperCase().replace(/^F0*/i, 'F') === normalizedPdf
      );
      if (!furnaceEntry) return;

      void batchApi.getActive(furnaceEntry.id).then((freshBatch) => {
        if (matchesSingle) setSingleBatch(freshBatch);
        if (matchesCombo) {
          setComboBatches(prev => ({
            ...prev,
            [normalizedPdf]: freshBatch,
            [rawNum]: freshBatch,
            [furnaceEntry.number.toUpperCase()]: freshBatch
          }));
        }
      }).catch(() => {/* batch not yet saved — ignore, SignalR ReceiveUpdate will cover it */});
    });

    return () => {
      unSubState();
      unSubSystemState();
      unSubUpdate();
      unSubClear();
      unSubPdf();
      unSubFurnaceStatus();
    };
  }, [selectedView, isCombo, targetFurnaceNumbers, furnaces]);

  const toggleFullscreen = () => {
    if (!document.fullscreenElement) {
      void document.documentElement.requestFullscreen();
    } else {
      if (document.exitFullscreen) {
        void document.exitFullscreen();
      }
    }
  };

  const renderBatchNumber = (batch?: string) => {
    if (!batch) return '—';
    const parts = batch.split('/');
    if (parts.length > 1) {
      return (
        <>
          <span style={{ color: '#2563EB' }}>{parts[0]}</span> / {parts.slice(1).join('/')}
        </>
      );
    }
    return <span style={{ color: '#2563EB' }}>{batch}</span>;
  };

  const mode = useMemo(() => statusMode(singleBatch?.indicator), [singleBatch?.indicator]);
  const meta = useMemo(() => parseBatchMetadata(singleBatch?.remarks), [singleBatch?.remarks]);

  const singleCards = useMemo(() => {
    const baseCards = [
      {
        label: 'SILICON',
        symbol: 'Si',
        value: singleBatch?.si ?? 0,
        unit: meta.units.si || 'g',
      },
      {
        label: 'TITANIUM BORON',
        symbol: 'AlTiB',
        value: singleBatch?.ti ?? 0,
        unit: meta.units.ti || 'g',
      },
      {
        label: 'MAGNESIUM',
        symbol: 'Mg',
        value: singleBatch?.mg ?? 0,
        unit: meta.units.mg || 'g',
      },
      {
        label: 'ALUMINUM STRONTIUM',
        symbol: 'AlSr',
        value: singleBatch?.sr ?? singleBatch?.futureChemical1 ?? 0,
        unit: meta.units.sr || 'g',
      },
    ];

    const extraCards = meta.params.map((p) => ({
      label: p.name.toUpperCase(),
      symbol: p.name.slice(0, 3).toUpperCase(),
      value: Number(p.value) || 0,
      unit: p.unit || 'g',
    }));

    return [...baseCards, ...extraCards];
  }, [singleBatch?.ti, singleBatch?.mg, singleBatch?.sr, singleBatch?.si, singleBatch?.futureChemical1, meta]);

  const singleSwatches = getColorSwatches(singleBatch?.colorCode);

  // Check if any furnace from F21 to F25 currently has an active batch
  const hasActiveBatchIn21To25 = useMemo(() => {
    const extraFurnaces = ['F21', 'F22', 'F23', 'F24', 'F25'];
    return extraFurnaces.some(num => {
      const raw = num.replace(/^F/i, '');
      const b = comboBatches[num] || comboBatches[raw];
      if (b && b.batchNumber && b.batchNumber.trim() !== '') return true;
      const fObj = furnaces.find(
        (f) =>
          f.number.toUpperCase() === num.toUpperCase() ||
          f.number.replace(/^F/i, '') === raw
      );
      if (fObj && (fObj.activeBatchId || (fObj.activeBatchNumber && fObj.activeBatchNumber.trim() !== ''))) return true;
      return false;
    });
  }, [comboBatches, furnaces]);

  // When COMBO_ALL is selected: show 1-20 initially; if any batch is active in 21-25, show 1-25
  const displayFurnaceNumbers = useMemo(() => {
    if (selectedView === 'COMBO_ALL') {
      return hasActiveBatchIn21To25
        ? Array.from({ length: 25 }, (_, i) => `F${i + 1}`)
        : Array.from({ length: 20 }, (_, i) => `F${i + 1}`);
    }
    return targetFurnaceNumbers;
  }, [selectedView, targetFurnaceNumbers, hasActiveBatchIn21To25]);

  // Render Combo Overview View if in Combo Mode
  if (isCombo) {
    const comboTitleMap: Record<string, string> = {
      COMBO_1_7: 'COMBO DISPLAY — FURNACES 1 TO 7',
      COMBO_8_14: 'COMBO DISPLAY — FURNACES 8 TO 14',
      COMBO_15_20: 'COMBO DISPLAY — FURNACES 15 TO 20',
      COMBO_21_25: 'COMBO DISPLAY — FURNACES 21 TO 25',
      COMBO_ALL: hasActiveBatchIn21To25 ? 'ALL FURNACES (1 TO 25)' : 'ALL FURNACES (1 TO 20)',
    };
    // Robust feature detection: if the browser does NOT support CSS Grid, use the TV flexbox fallback.
    const supportsGrid = typeof CSS !== 'undefined' && CSS.supports && CSS.supports('display', 'grid');
    const isTvBrowser = !supportsGrid || /SmartTV|Vewd|Opera TV|Presto|Sony|NetCast|Web0S/i.test(navigator.userAgent);

    return (
      <div className={`combo-shell ${isTvBrowser ? 'is-tv-browser' : ''}`} data-theme={isDarkMode ? 'dark' : 'light'}>
        <header className="combo-header">
          {/* Left: Brand Identity (Logo + Name + Screen Title) */}
          <div style={{ display: 'flex', alignItems: 'center', gap: '14px' }}>
            {/* Logo Emblem Container */}
            <div
              style={{
                background: isDarkMode
                  ? 'linear-gradient(135deg, rgba(255, 255, 255, 0.09) 0%, rgba(255, 255, 255, 0.03) 100%)'
                  : 'linear-gradient(135deg, #FFFFFF 0%, #F8FAFC 100%)',
                border: isDarkMode
                  ? '1px solid rgba(0, 229, 255, 0.3)'
                  : '1px solid rgba(37, 99, 235, 0.2)',
                borderRadius: '12px',
                padding: '6px 14px',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                boxShadow: isDarkMode
                  ? '0 4px 18px rgba(0, 0, 0, 0.45), 0 0 16px rgba(0, 229, 255, 0.18)'
                  : '0 2px 10px rgba(0, 0, 0, 0.06)',
                backdropFilter: 'blur(10px)',
                height: '46px',
                flexShrink: 0,
                transition: 'all 0.25s ease',
              }}
            >
              <img
                src="/logo.png"
                alt="AMAC Logo"
                style={{
                  height: '34px',
                  width: 'auto',
                  objectFit: 'contain',
                  display: 'block',
                  filter: isDarkMode
                    ? 'brightness(1.35) drop-shadow(0 0 8px rgba(0, 229, 255, 0.45))'
                    : 'none',
                }}
                onError={(e) => {
                  const t = e.currentTarget;
                  if (t.src.endsWith('/logo.png')) { t.src = '/logo.svg'; }
                  else { t.style.display = 'none'; }
                }}
              />
            </div>

            {/* Radiant Divider */}
            <div
              style={{
                width: '2px',
                height: '44px',
                background: isDarkMode
                  ? 'linear-gradient(180deg, transparent 0%, rgba(0, 229, 255, 0.55) 50%, transparent 100%)'
                  : 'linear-gradient(180deg, transparent 0%, rgba(37, 99, 235, 0.4) 50%, transparent 100%)',
                flexShrink: 0,
              }}
            />

            {/* Typography Stack */}
            <div style={{ display: 'flex', flexDirection: 'column', gap: '3px' }}>
              {/* App Name & Plant Badge */}
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px', lineHeight: 1 }}>
                <span
                  style={{
                    fontSize: '0.75rem',
                    fontWeight: 800,
                    letterSpacing: '0.12em',
                    textTransform: 'uppercase',
                    color: isDarkMode ? '#00E5FF' : '#2563EB',
                    lineHeight: 1,
                  }}
                >
                  Chemistry Monitor
                </span>
                <span
                  style={{
                    fontSize: '0.62rem',
                    fontWeight: 700,
                    padding: '2px 7px',
                    borderRadius: '4px',
                    background: isDarkMode ? 'rgba(0, 229, 255, 0.14)' : 'rgba(37, 99, 235, 0.1)',
                    color: isDarkMode ? '#00E5FF' : '#2563EB',
                    border: isDarkMode ? '1px solid rgba(0, 229, 255, 0.35)' : '1px solid rgba(37, 99, 235, 0.25)',
                    letterSpacing: '0.06em',
                    textTransform: 'uppercase',
                    lineHeight: 1,
                  }}
                >
                  U-5
                </span>
              </div>

              {/* Main Screen Title */}
              <div
                className="combo-title"
                style={{
                  fontSize: '1.35rem',
                  fontWeight: 900,
                  letterSpacing: '0.02em',
                  textTransform: 'uppercase',
                  color: isDarkMode ? '#FFFFFF' : '#0F172A',
                  lineHeight: 1.15,
                  margin: 0,
                  padding: 0,
                  textShadow: isDarkMode ? '0 2px 10px rgba(0,0,0,0.5)' : 'none',
                }}
              >
                {comboTitleMap[selectedView] || 'COMBO DISPLAY'}
              </div>

              {/* Subtitle */}
              <div
                className="combo-subtitle"
                style={{
                  fontSize: '0.72rem',
                  color: isDarkMode ? '#94A3B8' : '#64748B',
                  display: 'flex',
                  alignItems: 'center',
                  gap: '6px',
                  margin: 0,
                  lineHeight: 1,
                }}
              >
                <span
                  style={{
                    width: '6px',
                    height: '6px',
                    borderRadius: '50%',
                    background: '#10B981',
                    display: 'inline-block',
                    boxShadow: '0 0 6px #10B981',
                  }}
                />
                <span>Live Plant Monitoring · {displayFurnaceNumbers.length} Furnaces</span>
              </div>
            </div>
          </div>

          <div className="d-flex align-items-center gap-3">
            <span className={`footer-live ${connected && systemLive ? 'is-live' : ''}`}>
              {connected && systemLive ? 'LIVE BROADCAST' : 'OFFLINE'}
            </span>
            <div className="tv-controls align-items-center">
              <button 
                className="btn btn-secondary fw-bold text-uppercase d-flex align-items-center me-2"
                style={{ borderRadius: '8px', padding: '0.4rem 1rem', fontSize: '0.9rem' }}
                onClick={handlePrevView}
              >
                <i className="bi bi-chevron-left me-1"></i> Prev
              </button>
              <button 
                className="btn btn-primary fw-bold text-uppercase d-flex align-items-center"
                style={{ borderRadius: '8px', padding: '0.4rem 1rem', fontSize: '0.9rem' }}
                onClick={handleNextView}
              >
                Next <i className="bi bi-chevron-right ms-1"></i>
              </button>
            </div>
            
            <select
              className="display-select pc-controls"
              value={selectedView}
              onChange={(e) => {
                setSelectedView(e.target.value);
                localStorage.setItem('displaySelectedView', e.target.value);
              }}
            >
              <optgroup label="Single Furnace Display">
                {furnaces.map((f) => (
                  <option key={f.id} value={f.number}>
                    Furnace {formatFurnaceNumber(f.number)}
                  </option>
                ))}
              </optgroup>
              <optgroup label="Emergency Combo Displays">
                <option value="COMBO_1_7">Combo: 1 to 7 (Group 1)</option>
                <option value="COMBO_8_14">Combo: 8 to 14 (Group 2)</option>
                <option value="COMBO_15_20">Combo: 15 to 20 (Group 3)</option>
                <option value="COMBO_21_25">Combo: 21 to 25 (Group 4)</option>
                <option value="COMBO_ALL">
                  {hasActiveBatchIn21To25 ? 'Combo: All Furnaces (1 to 25)' : 'Combo: All Furnaces (1 to 20)'}
                </option>
              </optgroup>
            </select>
            <button 
              className="btn btn-outline-secondary d-flex align-items-center ms-2"
              onClick={toggleTheme}
              style={{ borderRadius: '8px', padding: '0.4rem 0.8rem', fontSize: '0.9rem', color: isDarkMode ? '#eab308' : '#3b82f6', borderColor: isDarkMode ? '#eab308' : '#3b82f6' }}
              title="Toggle Theme"
            >
              <i className={`bi ${isDarkMode ? 'bi-sun-fill' : 'bi-moon-fill'} me-2`}></i> Mode
            </button>
            <button className="display-fullscreen ms-2" onClick={toggleFullscreen}>
              {isFullscreen ? 'Exit Fullscreen' : 'Fullscreen'}
            </button>
          </div>
        </header>

        {(() => {
          return (
            <main
              className={`combo-grid ${
                selectedView === 'COMBO_21_25'
                  ? 'grid-5'
                  : selectedView === 'COMBO_15_20'
                    ? 'grid-6'
                    : selectedView === 'COMBO_ALL'
                      ? (hasActiveBatchIn21To25 ? 'grid-25' : 'grid-20 items-20')
                      : 'grid-7'
              }`}
            >
              {displayFurnaceNumbers.map((fNum) => {
            const canonical = fNum.toUpperCase().startsWith('F') ? fNum.toUpperCase() : `F${fNum}`;
            const raw = canonical.replace(/^F/i, '');
            const b = comboBatches[canonical] || comboBatches[raw] || comboBatches[fNum];

            // If no active batch for this furnace, show a rich operational status card
            if (!b) {
              const fObj = furnaces.find(
                (f) =>
                  f.number.toUpperCase() === fNum.toUpperCase() ||
                  f.number.replace(/^F/i, '') === fNum.replace(/^F/i, ''),
              );
              const opConfig = getOperationalStatusConfig(fObj?.operationalStatus);

              return (
                <div
                  key={fNum}
                  className={`combo-furnace-card ${opConfig.cardClass}`}
                >
                  <div className="combo-card-header">
                    <div className="combo-furnace-badge">{formatFurnaceNumber(fNum)}</div>
                    <span
                      className={`combo-status-badge ${opConfig.badgeClass} d-inline-flex align-items-center gap-1`}
                      style={opConfig.badgeStyle}
                    >
                      <i className={`bi ${opConfig.icon}`}></i>
                      <span>{opConfig.badgeText}</span>
                    </span>
                  </div>
                  <div className="combo-batch-line">
                    <span>Batch: <strong className="combo-batch-no">—</strong></span>
                  </div>
                  <div
                    className="combo-param-container"
                    style={{
                      justifyContent: 'center',
                      alignItems: 'center',
                      minHeight: selectedView === 'COMBO_ALL' ? '28px' : '80px',
                      flexDirection: 'column',
                    }}
                  >
                    <div
                      style={{
                        color: opConfig.color,
                        fontSize: selectedView === 'COMBO_ALL' ? '0.85rem' : '1.05rem',
                        fontWeight: 700,
                        textAlign: 'center',
                        display: 'flex',
                        alignItems: 'center',
                        gap: '6px',
                      }}
                    >
                      <i className={`bi ${opConfig.icon}`} style={{ fontSize: selectedView === 'COMBO_ALL' ? '1rem' : '1.3rem', lineHeight: 1 }}></i>
                      <span>{opConfig.title}</span>
                    </div>
                    {fObj?.operationalRemarks && fObj.operationalRemarks.trim() && (
                      <div
                        style={{
                          color: 'var(--theme-text-secondary)',
                          fontSize: selectedView === 'COMBO_ALL' ? '0.65rem' : '0.75rem',
                          textAlign: 'center',
                          marginTop: '2px',
                          maxWidth: '92%',
                          overflow: 'hidden',
                          textOverflow: 'ellipsis',
                          whiteSpace: 'nowrap',
                        }}
                      >
                        Note: {fObj.operationalRemarks.trim()}
                      </div>
                    )}
                  </div>
                  <div
                    className="combo-instruction-large"
                    style={{
                      backgroundColor: 'var(--theme-instruction-bg)',
                      color: opConfig.color,
                      borderLeftColor: opConfig.color,
                    }}
                  >
                    <span>{opConfig.instruction}</span>
                  </div>
                </div>
              );
            }

            const cMode = statusMode(b?.indicator);
            const cMeta = parseBatchMetadata(b?.remarks);
            const cSwatches = getColorSwatches(b?.colorCode);

            const siVal = b?.si ?? 0;
            const tiVal = b?.ti ?? 0;
            const mgVal = b?.mg ?? 0;
            const srVal = b?.sr ?? b?.futureChemical1 ?? 0;

            return (
              <div key={fNum} className={`combo-furnace-card ${cMode}`}>
                <div className="combo-card-header">
                  <div className="combo-furnace-badge">
                    {formatFurnaceNumber(fNum)}
                    {cMeta.weightText && <span className="ms-2 badge" style={{ backgroundColor: 'var(--theme-accent)', color: 'var(--theme-accent-contrast)', fontSize: '0.55em', padding: '0.35em 0.6em', verticalAlign: 'middle' }}>{cMeta.weightText}</span>}
                  </div>
                  <span
                    className={`combo-status-badge ${
                      b
                        ? b.indicator === 3
                          ? 'bg-danger text-white'
                          : b.indicator === 2
                            ? 'bg-warning text-dark'
                            : 'bg-success text-white'
                        : 'bg-secondary text-white'
                    }`}
                  >
                    {b ? (
                      b.indicator === 3 ? (
                        <>
                          <i className="bi bi-sign-stop-fill me-1"></i> RED
                        </>
                      ) : b.indicator === 2 ? (
                        <>
                          <i className="bi bi-bell-fill me-1"></i> YELLOW
                        </>
                      ) : (
                        <>
                          <i className="bi bi-check2-circle me-1"></i> GREEN
                        </>
                      )
                    ) : (
                      'NO BATCH'
                    )}
                  </span>
                </div>

                <div className="combo-batch-line">
                  <span>Batch: <strong className="combo-batch-no">{b?.batchNumber || '—'}</strong></span>
                  <div className="combo-color-box">
                    <span>Color:</span>
                    <div className="combo-color-strip">
                      {cSwatches.map((color, idx) => (
                        <span key={idx} className="combo-swatch" style={{ backgroundColor: color }} />
                      ))}
                    </div>
                  </div>
                </div>

                <div className="combo-param-container">
                  {(() => {
                    const overSpecChemicals = (cMeta.calculationDetails || [])
                        .filter(c => c.formula?.includes('Batch Rejected') || (c.maxSpec > 0 && c.actualValue > c.maxSpec && c.formula !== 'In Specification'))
                        .map(c => ({
                            id: `overspec-${c.chemical}`,
                            name: c.chemical,
                            value: 0,
                            unit: '',
                            cls: 'param-overspec',
                            isOverSpec: true
                        }));

                    const isRejected = overSpecChemicals.length > 0;

                    if (isRejected) {
                      return (
                        <div className="d-flex flex-column align-items-center justify-content-center p-3 rounded h-100 w-100" style={{ minHeight: '120px' }}>
                          <div className="fw-bold text-danger text-center fs-3 d-flex align-items-center justify-content-center gap-2">
                            <i className="bi bi-x-octagon-fill"></i> BATCH REJECTED
                          </div>
                        </div>
                      );
                    }

                    const activeParams = [
                      { id: 'si', name: 'Si', value: siVal, unit: cMeta.units.si || 'g', cls: 'param-si' },
                      { id: 'ti', name: 'AlTiB', value: tiVal, unit: cMeta.units.ti || 'g', cls: 'param-ti' },
                      { id: 'mg', name: 'Mg', value: mgVal, unit: cMeta.units.mg || 'g', cls: 'param-mg' },
                      { id: 'sr', name: 'AlSr', value: srVal, unit: cMeta.units.sr || 'g', cls: 'param-sr' },
                      ...cMeta.params.map(p => ({ id: p.id, name: p.name, value: Number(p.value) || 0, unit: p.unit || 'g', cls: '' }))
                    ].filter(p => p.value > 0).map(p => ({...p, isOverSpec: false}));

                    const allDisplayParams = [...activeParams];

                    if (allDisplayParams.length === 0) {
                      return (
                        <div className="d-flex flex-column align-items-center justify-content-center p-3 rounded" style={{ backgroundColor: 'rgba(16, 185, 129, 0.1)', border: '1px solid #10B981', minHeight: '120px', position: 'relative', overflow: 'hidden' }}>
                          <TvBackgroundVideo src="/success-video.mp4" opacity={0.65} />
                          <div style={{ position: 'relative', zIndex: 1, display: 'flex', flexDirection: 'column', alignItems: 'center' }}>
                            <div style={{ color: '#10B981', fontSize: '2rem', lineHeight: 1, textShadow: '0 2px 4px rgba(0,0,0,0.8)' }}>
                              <i className="bi bi-check2-circle"></i>
                            </div>
                            <div className="fw-bold text-white mt-1" style={{ textShadow: '0 2px 4px rgba(0,0,0,0.8)' }}>Everything is Good</div>
                          </div>
                        </div>
                      );
                    }

                    return (
                      <div className="combo-param-row-grid">
                        {allDisplayParams.map(p => (
                          <div key={p.id} className={`combo-param-card-large ${p.cls}`}>
                            <div className="combo-param-symbol-large">{p.name}</div>
                            <div className="combo-param-value-large">{formatWeightValue(p.value)}</div>
                            <div className="combo-param-unit-large">{formatWeightUnit(p.value, p.unit)}</div>
                          </div>
                        ))}
                      </div>
                    );
                  })()}
                </div>

                <div 
                  className="combo-instruction-large"
                  style={{
                    backgroundColor: (b?.instruction === 'Contact Lab Batch Rejected' || b?.instruction === 'Contact Lab' || (cMeta.calculationDetails || []).some(c => c.formula?.includes('Batch Rejected') || (c.maxSpec > 0 && c.actualValue > c.maxSpec && c.formula !== 'In Specification'))) ? '#dc2626' : 
                                     b?.instruction === 'Start Pouring' ? '#16a34a' :
                                     b?.instruction === 'Add these chemicals' ? '#eab308' : 'var(--theme-instruction-bg)',
                    color: (b?.instruction === 'Contact Lab Batch Rejected' || b?.instruction === 'Contact Lab' || (cMeta.calculationDetails || []).some(c => c.formula?.includes('Batch Rejected') || (c.maxSpec > 0 && c.actualValue > c.maxSpec && c.formula !== 'In Specification')) || b?.instruction === 'Start Pouring') ? '#ffffff' :
                           b?.instruction === 'Add these chemicals' ? '#18181b' : 'var(--theme-text-primary)',
                    borderLeftColor: (b?.instruction === 'Contact Lab Batch Rejected' || b?.instruction === 'Contact Lab' || (cMeta.calculationDetails || []).some(c => c.formula?.includes('Batch Rejected') || (c.maxSpec > 0 && c.actualValue > c.maxSpec && c.formula !== 'In Specification'))) ? '#b91c1c' : 
                                     b?.instruction === 'Start Pouring' ? '#15803d' :
                                     b?.instruction === 'Add these chemicals' ? '#ca8a04' : 'var(--theme-accent)'
                  }}
                >
                  <span>Instruct: </span>
                  <strong>{((cMeta.calculationDetails || []).some(c => c.formula?.includes('Batch Rejected') || (c.maxSpec > 0 && c.actualValue > c.maxSpec && c.formula !== 'In Specification'))) ? 'Contact Lab Batch Rejected' : (b?.instruction || 'None')}</strong>
                </div>
              </div>
            );
          })}
            </main>
          );
        })()}
      </div>
    );
  }

  // Single Furnace View
  const currentFurnaceObj = furnaces.find(
    (f) =>
      f.number.toUpperCase() === selectedView.toUpperCase() ||
      f.number.replace(/^F/i, '') === selectedView.replace(/^F/i, ''),
  );
  const singleOpConfig = getOperationalStatusConfig(currentFurnaceObj?.operationalStatus);

  return (
    <div className={`display-shell ${mode}`} data-theme={isDarkMode ? 'dark' : 'light'} style={{ flex: 1, overflow: 'hidden' }}>
      
      {/* --- MOBILE SPECIFIC HEADER --- */}
      <div className="mobile-top-card d-block d-md-none">
        <div className="d-flex justify-content-between align-items-center mb-1">
          <div className="fs-4 fw-bold d-flex align-items-center gap-2">

            <span>FURNACE {formatFurnaceNumber(selectedView)}</span>
          </div>
          <div
            className="badge rounded-pill px-2 py-1"
            style={{
              backgroundColor: singleBatch ? '#D1FAE5' : `${singleOpConfig.color}25`,
              color: singleBatch ? '#065F46' : singleOpConfig.color,
              fontSize: '0.75rem',
            }}
          >
            Status: {singleBatch ? 'Operating' : singleOpConfig.title}
          </div>
        </div>
        
        <div className="d-flex justify-content-between align-items-center mb-2">
          <div className="d-flex align-items-center gap-2">
            <span className="text-muted fw-bold text-uppercase" style={{ fontSize: '0.65rem', letterSpacing: '0.05em' }}>Weight</span>
            <span className="badge rounded-pill px-2 py-1 shadow-sm" style={{ backgroundColor: '#4773B3', color: '#fff', fontSize: '0.8rem' }}>
              {meta.weightText || 'N/A'}
            </span>
          </div>
          <div className="d-flex align-items-center gap-2">
            <span className="text-muted fw-bold text-uppercase" style={{ fontSize: '0.65rem', letterSpacing: '0.05em' }}>Color</span>
            <div className="shadow-sm" style={{ backgroundColor: singleSwatches[0], width: '40px', height: '16px', borderRadius: '4px' }}></div>
          </div>
        </div>

        <div className="d-flex justify-content-between align-items-center">
          <span className="text-muted fw-bold text-uppercase" style={{ fontSize: '0.65rem', letterSpacing: '0.05em' }}>Batch</span>
          <span className="fs-4 fw-bold" style={{ color: '#111827' }}>{renderBatchNumber(singleBatch?.batchNumber)}</span>
        </div>
        
        <div className="text-end text-muted mt-1" style={{ fontSize: '0.6rem' }}>
          Last Updated: {singleBatch 
              ? new Date(singleBatch.startedAtUtc.endsWith('Z') || singleBatch.startedAtUtc.includes('+') ? singleBatch.startedAtUtc : singleBatch.startedAtUtc + 'Z').toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit', hour12: false, timeZone: 'Asia/Kolkata' }) 
              : '--:--:--'}
        </div>
      </div>
      {/* ------------------------------ */}

      <header className="display-header d-none d-md-flex">
        <div className="display-furnace d-flex align-items-center flex-wrap gap-3">
          {/* Logo | Divider | CHEMISTRY MONITOR + FURNACE NUMBER */}
          <div style={{ display: 'flex', alignItems: 'center', gap: '14px' }}>
            <div
              style={{
                background: isDarkMode
                  ? 'linear-gradient(135deg, rgba(255, 255, 255, 0.09) 0%, rgba(255, 255, 255, 0.03) 100%)'
                  : 'linear-gradient(135deg, #FFFFFF 0%, #F8FAFC 100%)',
                border: isDarkMode
                  ? '1px solid rgba(0, 229, 255, 0.3)'
                  : '1px solid rgba(37, 99, 235, 0.2)',
                borderRadius: '12px',
                padding: '6px 14px',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                boxShadow: isDarkMode
                  ? '0 4px 18px rgba(0, 0, 0, 0.45), 0 0 16px rgba(0, 229, 255, 0.18)'
                  : '0 2px 10px rgba(0, 0, 0, 0.06)',
                backdropFilter: 'blur(10px)',
                height: '46px',
                flexShrink: 0,
              }}
            >
              <img
                src="/logo.png"
                alt="AMAC Logo"
                style={{
                  height: '34px',
                  width: 'auto',
                  objectFit: 'contain',
                  display: 'block',
                  filter: isDarkMode
                    ? 'brightness(1.35) drop-shadow(0 0 8px rgba(0, 229, 255, 0.45))'
                    : 'none',
                }}
                onError={(e) => {
                  const t = e.currentTarget;
                  if (t.src.endsWith('/logo.png')) { t.src = '/logo.svg'; }
                  else { t.style.display = 'none'; }
                }}
              />
            </div>

            <div
              style={{
                width: '2px',
                height: '44px',
                background: isDarkMode
                  ? 'linear-gradient(180deg, transparent 0%, rgba(0, 229, 255, 0.55) 50%, transparent 100%)'
                  : 'linear-gradient(180deg, transparent 0%, rgba(37, 99, 235, 0.4) 50%, transparent 100%)',
                flexShrink: 0,
              }}
            />

            <div style={{ display: 'flex', flexDirection: 'column', gap: '3px' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px', lineHeight: 1 }}>
                <span
                  style={{
                    fontSize: '0.75rem',
                    fontWeight: 800,
                    letterSpacing: '0.12em',
                    textTransform: 'uppercase',
                    color: isDarkMode ? '#00E5FF' : '#2563EB',
                    lineHeight: 1,
                  }}
                >
                  Chemistry Monitor
                </span>
                <span
                  style={{
                    fontSize: '0.62rem',
                    fontWeight: 700,
                    padding: '2px 7px',
                    borderRadius: '4px',
                    background: isDarkMode ? 'rgba(0, 229, 255, 0.14)' : 'rgba(37, 99, 235, 0.1)',
                    color: isDarkMode ? '#00E5FF' : '#2563EB',
                    border: isDarkMode ? '1px solid rgba(0, 229, 255, 0.35)' : '1px solid rgba(37, 99, 235, 0.25)',
                    letterSpacing: '0.06em',
                    textTransform: 'uppercase',
                    lineHeight: 1,
                  }}
                >
                  U-5
                </span>
              </div>
              <span
                style={{
                  fontSize: '1.45rem',
                  fontWeight: 900,
                  letterSpacing: '0.03em',
                  textTransform: 'uppercase',
                  color: isDarkMode ? '#FFFFFF' : '#0F172A',
                  lineHeight: 1.1,
                  textShadow: isDarkMode ? '0 2px 10px rgba(0,0,0,0.5)' : 'none',
                }}
              >
                FURNACE {formatFurnaceNumber(selectedView)}
              </span>
            </div>
          </div>

          {meta.weightText && (
            <div className="d-flex align-items-center gap-2 ms-2">
              <span className="fs-6 fw-bold" style={{ color: '#6B7280', textTransform: 'uppercase', letterSpacing: '0.08em' }}>Weight</span>
              <span className="badge fs-5 px-3 py-1 rounded-pill" style={{ backgroundColor: '#2563EB', color: '#FFF' }}>
                {meta.weightText}
              </span>
            </div>
          )}
        </div>

        <div className="display-color-box">
          <div className="display-color-title">
            COLOR CODE: <span className="pc-only-color-name fw-bold" style={{ color: '#111827' }}>{singleBatch?.colorCode || 'Yellow-blue'}</span>
          </div>
          <div className="display-color-strip">
            {singleSwatches.map((color, idx) => (
              <span key={idx} className="display-color-swatch" style={{ backgroundColor: color }} />
            ))}
          </div>
        </div>

        <div className="display-status-block">
          <span
            className={`combo-status-badge ${
              singleBatch
                ? singleBatch.indicator === 3
                  ? 'bg-danger text-white'
                  : singleBatch.indicator === 2
                    ? 'bg-warning text-dark'
                    : 'bg-success text-white'
                : singleOpConfig.badgeClass
            }`}
            style={{
              fontSize: '1rem',
              padding: '0.5rem 1.2rem',
              borderRadius: '8px',
              ...(singleBatch ? {} : singleOpConfig.badgeStyle),
            }}
          >
            {singleBatch ? (
              singleBatch.indicator === 3 ? (
                <>
                  <i className="bi bi-sign-stop-fill me-2"></i> RED EMERGENCY
                </>
              ) : singleBatch.indicator === 2 ? (
                <>
                  <i className="bi bi-bell-fill me-2"></i> YELLOW CAUTION
                </>
              ) : (
                <>
                  <i className="bi bi-check2-circle me-2"></i> GREEN OPERATING
                </>
              )
            ) : (
              <>
                <i className={`bi ${singleOpConfig.icon} me-2`}></i> {singleOpConfig.badgeText}
              </>
            )}
          </span>
          <div className="display-status-update ms-3" style={{ color: '#6B7280' }}>
            Last Update:{' '}
            {singleBatch
              ? new Date(
                  singleBatch.startedAtUtc.endsWith('Z') || singleBatch.startedAtUtc.includes('+')
                    ? singleBatch.startedAtUtc
                    : singleBatch.startedAtUtc + 'Z',
                ).toLocaleTimeString([], {
                  hour: '2-digit',
                  minute: '2-digit',
                  second: '2-digit',
                  hour12: false,
                  timeZone: 'Asia/Kolkata',
                })
              : currentFurnaceObj?.operationalStatusUpdatedAtUtc
                ? new Date(
                    currentFurnaceObj.operationalStatusUpdatedAtUtc.endsWith('Z') ||
                    currentFurnaceObj.operationalStatusUpdatedAtUtc.includes('+')
                      ? currentFurnaceObj.operationalStatusUpdatedAtUtc
                      : currentFurnaceObj.operationalStatusUpdatedAtUtc + 'Z',
                  ).toLocaleTimeString([], {
                    hour: '2-digit',
                    minute: '2-digit',
                    second: '2-digit',
                    hour12: false,
                    timeZone: 'Asia/Kolkata',
                  })
                : '--:--:--'}
          </div>
        </div>
      </header>

      <div className="display-batch-header pc-only-batch d-md-flex">
        <div className="display-batch-label">ACTIVE BATCH</div>
        <div className="display-batch-value-centered">{renderBatchNumber(singleBatch?.batchNumber)}</div>
      </div>

      {/* High-Visibility Neon Chemical Grid */}
      <main className="display-grid">
        {(() => {
          if (!singleBatch) {
            return (
              <div
                className="display-card"
                style={
                  {
                    gridColumn: '1 / -1',
                    '--card-color': singleOpConfig.color,
                    '--card-border-color': singleOpConfig.color,
                    '--card-title-bg': `${singleOpConfig.color}15`,
                    '--card-glow-color': `${singleOpConfig.color}40`,
                    '--card-glow-shadow': `0 10px 30px -4px ${singleOpConfig.color}25`,
                    display: 'flex',
                    flexDirection: 'column',
                    justifyContent: 'center',
                    alignItems: 'center',
                    minHeight: '35vh',
                    padding: '3rem 2rem',
                    borderRadius: '16px',
                  } as React.CSSProperties
                }
              >
                <div style={{ fontSize: '5rem', color: singleOpConfig.color, marginBottom: '1rem', lineHeight: 1 }}>
                  <i className={`bi ${singleOpConfig.icon}`}></i>
                </div>
                <h2 className="text-white fw-bold display-4 mb-3">{singleOpConfig.title}</h2>
                {currentFurnaceObj?.operationalRemarks && (
                  <div
                    className="badge px-4 py-2 fs-6 mb-3"
                    style={{
                      backgroundColor: 'rgba(255, 255, 255, 0.08)',
                      color: '#e2e8f0',
                      border: `1px solid ${singleOpConfig.color}60`,
                    }}
                  >
                    Operator Note: {currentFurnaceObj.operationalRemarks}
                  </div>
                )}
                <div
                  className="badge rounded-pill px-4 py-2 mt-2 d-inline-flex align-items-center gap-2"
                  style={{
                    backgroundColor: `${singleOpConfig.color}20`,
                    color: singleOpConfig.color,
                    fontSize: '1.05rem',
                    border: `1px solid ${singleOpConfig.color}`,
                  }}
                >
                  <i className={`bi ${singleOpConfig.icon}`}></i>
                  <span>FURNACE {formatFurnaceNumber(selectedView)} · {singleOpConfig.badgeText}</span>
                </div>
              </div>
            );
          }

          const overSpecCards = (meta.calculationDetails || [])
              .filter(c => c.formula?.includes('Batch Rejected') || (c.maxSpec > 0 && c.actualValue > c.maxSpec && c.formula !== 'In Specification'))
              .map(c => ({
                  symbol: c.chemical,
                  label: c.chemical,
                  value: 0,
                  unit: '',
                  isOverSpec: true
              }));

          const activeCards = singleCards.filter((card) => card.value > 0).map(c => ({...c, isOverSpec: false}));
          const allDisplayCards = [...activeCards, ...overSpecCards];

          const isRejected = overSpecCards.length > 0;

          if (isRejected) {
            return (
              <div
                className="display-card display-card-fixed-center"
                style={
                  {
                    gridColumn: '1 / -1',
                    '--card-color': '#dc2626',
                    '--card-border-color': '#dc2626',
                    '--card-title-bg': 'rgba(220, 38, 38, 0.15)',
                    '--card-glow-color': 'rgba(220, 38, 38, 0.4)',
                    '--card-glow-shadow': '0 10px 30px -4px rgba(220, 38, 38, 0.15)',
                    display: 'flex',
                    flexDirection: 'column',
                    justifyContent: 'center',
                    alignItems: 'center',
                    height: '100%',
                    width: '100%'
                  } as React.CSSProperties
                }
              >
                <div style={{ fontSize: '5rem', color: '#dc2626', marginBottom: '1rem', lineHeight: 1 }}>
                  <i className="bi bi-x-octagon-fill"></i>
                </div>
                <h2 className="text-white fw-bold display-4 mb-2">BATCH REJECTED</h2>
                <p className="fs-4 mb-0 text-danger">Contact lab for instructions</p>
              </div>
            );
          }

          if (allDisplayCards.length === 0) {
            return (
              <div
                className="display-card display-card-fixed-center"
                style={
                  {
                    gridColumn: '1 / -1',
                    '--card-color': '#10B981',
                    '--card-border-color': '#10B981',
                    '--card-title-bg': 'rgba(16, 185, 129, 0.15)',
                    '--card-glow-color': 'rgba(16, 185, 129, 0.4)',
                    '--card-glow-shadow': '0 10px 30px -4px rgba(16, 185, 129, 0.15)',
                    display: 'flex',
                    flexDirection: 'column',
                    justifyContent: 'center',
                    alignItems: 'center',
                    height: '100%',
                    width: '100%',
                    position: 'relative',
                    overflow: 'hidden'
                  } as React.CSSProperties
                }
              >
                <TvBackgroundVideo src="/success-video.mp4" opacity={0.65} />
                <div style={{ position: 'relative', zIndex: 1, display: 'flex', flexDirection: 'column', alignItems: 'center' }}>
                  <div style={{ fontSize: '5rem', color: '#10B981', marginBottom: '1rem', lineHeight: 1, textShadow: '0 4px 8px rgba(0,0,0,0.8)' }}>
                    <i className="bi bi-check2-circle"></i>
                  </div>
                  <h2 className="text-white fw-bold display-4 mb-2" style={{ textShadow: '0 4px 8px rgba(0,0,0,0.8)' }}>Everything is Good</h2>
                  <p className="fs-4 mb-0" style={{ color: '#10B981', textShadow: '0 4px 8px rgba(0,0,0,0.8)' }}>No chemical additions required</p>
                </div>
              </div>
            );
          }

          return allDisplayCards.map((card, idx) => {
            const cStyle = getChemicalCardStyle(card.symbol, idx);
            const cardStyle: React.CSSProperties = {
              '--card-color': card.isOverSpec ? '#dc2626' : cStyle.color,
              '--card-border-color': card.isOverSpec ? '#dc2626' : cStyle.border,
              '--card-title-bg': card.isOverSpec ? 'rgba(220, 38, 38, 0.15)' : cStyle.bgSubtle,
              '--card-glow-color': card.isOverSpec ? 'rgba(220, 38, 38, 0.4)' : cStyle.glow,
              '--card-glow-shadow': card.isOverSpec ? '0 10px 30px -4px rgba(220, 38, 38, 0.15)' : `0 10px 30px -4px ${cStyle.bgSubtle}`,
            } as React.CSSProperties;
            
            return (
              <div key={`${card.symbol}-${idx}`} className="display-card" style={cardStyle}>
                <div className="display-card-title-pill">{card.label}</div>
                <div className="display-card-symbol">{card.symbol}</div>
                <div className="display-card-content">
                  {card.isOverSpec ? (
                      <div className="display-card-value text-danger" style={{fontSize: '3rem', textAlign: 'center', lineHeight: '1.2'}}>Contact<br/>for Lab</div>
                  ) : (
                      <>
                        <div className="display-card-value">{formatWeightValue(card.value)}</div>
                        <div className="display-card-unit">{formatWeightUnit(card.value, card.unit)}</div>
                      </>
                  )}
                </div>
              </div>
            );
          });
        })()}
      </main>

      {singleBatch && (
        <div 
          className="display-instruction flex-column align-items-start"
          style={{
            background: (() => {
              const isRed = meta.calculationDetails?.some(c => c.formula?.includes('Batch Rejected') || (c.maxSpec > 0 && c.actualValue > c.maxSpec && c.formula !== 'In Specification'));
              if (isRed || singleBatch?.instruction === 'Contact Lab Batch Rejected' || singleBatch?.instruction === 'Contact Lab') return '#dc2626';
              if (singleBatch?.instruction === 'Start Pouring') return '#16a34a';
              if (singleBatch?.instruction === 'Add these chemicals') return '#eab308';
              
              const isOrange = singleCards.some(c => c.value > 0);
              if (isOrange) return '#F97316';
              return '#16a34a';
            })(),
            color: (meta.calculationDetails?.some(c => c.formula?.includes('Batch Rejected') || (c.maxSpec > 0 && c.actualValue > c.maxSpec && c.formula !== 'In Specification')) || singleBatch?.instruction === 'Contact Lab Batch Rejected' || singleBatch?.instruction === 'Contact Lab' || singleBatch?.instruction === 'Start Pouring') ? '#ffffff' :
                   singleBatch?.instruction === 'Add these chemicals' ? '#18181b' : '#ffffff'
          }}
        >
          <div className="w-100">
            <span style={{ color: (meta.calculationDetails?.some(c => c.formula?.includes('Batch Rejected') || (c.maxSpec > 0 && c.actualValue > c.maxSpec && c.formula !== 'In Specification')) || singleBatch?.instruction === 'Contact Lab Batch Rejected' || singleBatch?.instruction === 'Contact Lab' || singleBatch?.instruction === 'Start Pouring') ? 'rgba(255, 255, 255, 0.9)' :
                                 singleBatch?.instruction === 'Add these chemicals' ? 'rgba(0, 0, 0, 0.7)' : 'rgba(255, 255, 255, 0.9)' }}>OPERATOR INSTRUCTION:</span>
            <strong className="ms-2 fs-3">
              {meta.calculationDetails?.some(c => c.formula?.includes('Batch Rejected') || (c.maxSpec > 0 && c.actualValue > c.maxSpec && c.formula !== 'In Specification')) ? 'Contact Lab Batch Rejected' : (singleBatch?.instruction || '—')}
            </strong>
          </div>
          {meta.userRemarks && (
            <div className="mt-2 fs-4 w-100 border-top pt-2" style={{ borderColor: 'rgba(255,255,255,0.2)' }}>
              <span className="fw-bold me-2" style={{ color: 'rgba(255, 255, 255, 0.85)' }}>REMARKS:</span>
              <span className="text-white fw-medium">{meta.userRemarks}</span>
            </div>
          )}
        </div>
      )}

      <footer className="display-footer">
        <div className="display-footer-operator">
          <span>OPERATOR:</span> <strong>{singleBatch?.operatorName || 'SYSTEM ADMINISTRATOR'}</strong>
          <span className="display-live-indicator ms-2" />
        </div>

        <div className="display-footer-right">
          <span className={`footer-live ${connected && systemLive ? 'is-live' : ''} d-none d-md-inline`}>{connected && systemLive ? 'SYSTEM LIVE' : 'SYSTEM OFFLINE'}</span>
          <div className="display-toolbar d-flex align-items-center">
            <div className="tv-controls align-items-center d-flex">
              <button 
                className="btn btn-secondary fw-bold text-uppercase d-flex align-items-center me-2"
                style={{ borderRadius: '8px', padding: '0.4rem 1rem', fontSize: '0.9rem' }}
                onClick={handlePrevView}
              >
                <i className="bi bi-chevron-left me-1"></i> Prev
              </button>
              <button 
                className="btn btn-primary fw-bold text-uppercase d-flex align-items-center me-2"
                style={{ borderRadius: '8px', padding: '0.4rem 1rem', fontSize: '0.9rem' }}
                onClick={handleNextView}
              >
                Next <i className="bi bi-chevron-right ms-1"></i>
              </button>
            </div>
            
            <select
              className="display-select pc-controls"
              value={selectedView}
              onChange={(e) => {
                setSelectedView(e.target.value);
                localStorage.setItem('displaySelectedView', e.target.value);
              }}
            >
              <optgroup label="Single Furnace Display">
                {furnaces.map((f) => (
                  <option key={f.id} value={f.number}>
                    Furnace {formatFurnaceNumber(f.number)}
                  </option>
                ))}
              </optgroup>
              <optgroup label="Emergency Combo Displays">
                <option value="COMBO_1_7">Combo: 1 to 7 (Group 1)</option>
                <option value="COMBO_8_14">Combo: 8 to 14 (Group 2)</option>
                <option value="COMBO_15_20">Combo: 15 to 20 (Group 3)</option>
                <option value="COMBO_21_25">Combo: 21 to 25 (Group 4)</option>
                <option value="COMBO_ALL">
                  {hasActiveBatchIn21To25 ? 'Combo: All Furnaces (1 to 25)' : 'Combo: All Furnaces (1 to 20)'}
                </option>
              </optgroup>
            </select>
            <button 
              className="btn btn-outline-secondary d-flex align-items-center ms-2"
              onClick={toggleTheme}
              style={{ borderRadius: '8px', padding: '0.4rem 0.8rem', fontSize: '0.9rem', color: isDarkMode ? '#eab308' : '#3b82f6', borderColor: isDarkMode ? '#eab308' : '#3b82f6' }}
              title="Toggle Theme"
            >
              <i className={`bi ${isDarkMode ? 'bi-sun-fill' : 'bi-moon-fill'} me-2`}></i> Mode
            </button>
            <button 
              className="btn btn-outline-info d-flex align-items-center ms-2"
              onClick={() => alert(`Smart TV Direct LAN URL:\n\n${window.location.origin}/display\n\nType this link into your Smart TV browser.`)}
              style={{ borderRadius: '8px', padding: '0.4rem 0.8rem', fontSize: '0.9rem', borderColor: '#06b6d4', color: '#06b6d4' }}
              title="Smart TV LAN Link"
            >
              <i className="bi bi-broadcast me-1"></i> TV Link
            </button>
            <button className="display-fullscreen ms-2" onClick={toggleFullscreen}>
              {isFullscreen ? 'Exit Fullscreen' : 'Fullscreen'}
            </button>
          </div>
        </div>
      </footer>
    </div>
  );
}
