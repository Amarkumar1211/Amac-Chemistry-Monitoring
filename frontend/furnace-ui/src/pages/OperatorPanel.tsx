import { FormEvent, useCallback, useEffect, useState } from 'react';
import { batchApi, furnaceApi, dashboardApi } from '../services/api';
import { useAuth } from '../context/AuthContext';
import { realtimeClient } from '../services/signalr';
import { COLOR_CODE_OPTIONS, UNIT_OPTIONS } from '../types';
import type { BatchDto, FurnaceDto, IndicatorColor, ParsedSpectroResultDto, CalculationStepDto } from '../types';
import PdfUploader from '../components/PdfUploader';

const MAX_VALUE = 1000;

interface CustomParamRow {
  id: string;
  name: string;
  value: string;
  unit: string;
}

export default function OperatorPanel() {
  const { user } = useAuth();
  const [furnaces, setFurnaces] = useState<FurnaceDto[]>([]);
  const [selectedId, setSelectedId] = useState<number>(0);
  const [batch, setBatch] = useState<BatchDto | null>(null);
  const [loadingBatch, setLoadingBatch] = useState(false);
  const [message, setMessage] = useState<string>();

  // Batch Form State
  const [batchNumber, setBatchNumber] = useState('');

  // Dynamic Chemical Parameters
  const [customParams, setCustomParams] = useState<CustomParamRow[]>([]);
  const [calculationDetails, setCalculationDetails] = useState<CalculationStepDto[]>([]);
  const [actualValues, setActualValues] = useState<Record<string, number>>({});

  const [colorCode, setColorCode] = useState<string>('Yellow-blue');
  const [indicator, setIndicator] = useState<IndicatorColor>(1);
  const [instruction, setInstruction] = useState('');
  const [remarks, setRemarks] = useState('');
  const [weightText, setWeightText] = useState('');
  const [saving, setSaving] = useState(false);
  const [confirmClear, setConfirmClear] = useState(false);

  // PDF Parsing Error / Solution Alert
  const [pdfAlert, setPdfAlert] = useState<{
    title: string;
    mistake: string;
    solution: string;
    rawWarnings?: string[];
    extracted?: {
      furnaceNumber?: number | string | null;
      batchNumber?: string | null;
      partName?: string | null;
      partNo?: string | null;
      gradeName?: string | null;
      furnaceWeight?: number | null;
    };
  } | null>(null);


  const [statusRemarks, setStatusRemarks] = useState('');
  const [updatingStatus, setUpdatingStatus] = useState(false);

  // Operator Name Prompt
  const [showNamePrompt, setShowNamePrompt] = useState(false);
  const [operatorNameInput, setOperatorNameInput] = useState('');

  useEffect(() => {
    if (user?.role === 'Operator' || user?.username === 'operator') {
      const savedName = localStorage.getItem('actualOperatorName');
      if (!savedName) {
        setShowNamePrompt(true);
      }
    }
  }, [user]);

  const saveOperatorName = (e: FormEvent) => {
    e.preventDefault();
    if (!operatorNameInput.trim()) return;
    localStorage.setItem('actualOperatorName', operatorNameInput.trim());
    setShowNamePrompt(false);
  };

  const parseRemarksMetadata = (rawRemarks?: string) => {
    if (!rawRemarks) return null;
    try {
      if (rawRemarks.startsWith('{') && rawRemarks.endsWith('}')) {
        return JSON.parse(rawRemarks) as {
          userRemarks?: string;
          params?: CustomParamRow[];
          weightText?: string;
          calculationDetails?: CalculationStepDto[];
          actualValues?: Record<string, number>;
        };
      }
    } catch {
      // plain text remarks
    }
    return null;
  };

  const loadBatch = useCallback(async (furnaceId: number) => {
    if (!furnaceId) return;
    setLoadingBatch(true);
    try {
      const b = await batchApi.getActive(furnaceId);
      setBatch(b);
      if (b) {
        setBatchNumber('');
        setColorCode(b.colorCode || 'Yellow-blue');
        setIndicator(b.indicator);
        setInstruction(b.instruction ?? '');

        const meta = parseRemarksMetadata(b.remarks);
        setRemarks(meta?.userRemarks ?? b.remarks ?? '');
        setWeightText(meta?.weightText ?? '');
        setCalculationDetails(meta?.calculationDetails ?? []);
        setActualValues(meta?.actualValues ?? {});

        // Dynamically rebuild the parameter list from core chemicals and extra parameters
        const loadedParams: CustomParamRow[] = [
          { id: 'si', name: 'Si', value: String(b.si ?? 0), unit: 'g' },
          { id: 'ti', name: 'Ti', value: String(b.ti ?? 0), unit: 'g' },
          { id: 'mg', name: 'Mg', value: String(b.mg ?? 0), unit: 'g' },
          { id: 'sr', name: 'Sr', value: String(b.sr ?? 0), unit: 'g' },
        ];
        if (meta?.params) {
          meta.params.forEach(p => {
            const lower = p.name.toLowerCase();
            if (!['si', 'ti', 'mg', 'sr'].includes(lower)) {
              loadedParams.push(p);
            }
          });
        }
        setCustomParams(loadedParams);

      } else {
        setBatchNumber('');
        setColorCode('Yellow-blue');
        setIndicator(1);
        setInstruction('');
        setRemarks('');
        setWeightText('');
        setCustomParams([
          { id: 'si', name: 'Si', value: '0', unit: 'g' },
          { id: 'ti', name: 'Ti', value: '0', unit: 'g' },
          { id: 'mg', name: 'Mg', value: '0', unit: 'g' },
          { id: 'sr', name: 'Sr', value: '0', unit: 'g' },
        ]);
        setCalculationDetails([]);
        setActualValues({});
      }
    } catch (err) {
      setMessage((err as Error).message);
    } finally {
      setLoadingBatch(false);
    }
  }, []);

  const loadFurnaces = useCallback(async () => {
    try {
      const list = await furnaceApi.getAll();
      const active = list.filter((f) => f.isActive);
      setFurnaces(active);
      if (active.length > 0) {
        setSelectedId(active[0].id);
        void loadBatch(active[0].id);
      }
    } catch (err) {
      setMessage((err as Error).message);
    }
  }, [loadBatch]);

  useEffect(() => {
    void loadFurnaces();
  }, [loadFurnaces]);

  const getParamValue = (name: string): string => {
    const p = customParams.find((x) => x.name.toLowerCase() === name.toLowerCase());
    return p ? p.value : '0';
  };

  const setParamValue = (name: string, val: string, defaultUnit: string = 'g') => {
    setCustomParams((prev) => {
      const idx = prev.findIndex((x) => x.name.toLowerCase() === name.toLowerCase());
      if (idx >= 0) {
        return prev.map((item, i) => (i === idx ? { ...item, value: val } : item));
      } else {
        return [...prev, { id: name.toLowerCase(), name, value: val, unit: defaultUnit }];
      }
    });
  };

  const extractCoreChemicals = (params: CustomParamRow[] = customParams) => {
    let si = 0, ti = 0, mg = 0, sr = 0;
    const remainingParams: CustomParamRow[] = [];

    params.forEach(p => {
      const name = p.name.toLowerCase();
      const numVal = parseFloat(p.value) || 0;
      if (name === 'si') si = numVal;
      else if (name === 'ti') ti = numVal;
      else if (name === 'mg') mg = numVal;
      else if (name === 'sr') sr = numVal;
      else remainingParams.push(p);
    });

    return { si, ti, mg, sr, remainingParams };
  };

  const buildRemarksPayload = (userText: string, remainingParams: CustomParamRow[], wText: string, calcDetails: CalculationStepDto[] = calculationDetails, aValues: Record<string, number> = actualValues) => {
    if (!userText && remainingParams.length === 0 && !wText && calcDetails.length === 0 && Object.keys(aValues).length === 0) return '';
    return JSON.stringify({
      userRemarks: userText,
      params: remainingParams,
      weightText: wText,
      calculationDetails: calcDetails,
      actualValues: aValues
    });
  };

  const addParameterRow = () => {
    setCustomParams((prev) => [
      ...prev,
      { id: String(Date.now()), name: `Chemical ${prev.length + 1}`, value: '0', unit: 'g' },
    ]);
  };

  const removeParameterRow = (id: string) => {
    setCustomParams((prev) => prev.filter((p) => p.id !== id));
  };

  const updateParameterRow = (id: string, field: 'name' | 'value' | 'unit', val: string) => {
    setCustomParams((prev) =>
      prev.map((p) => (p.id === id ? { ...p, [field]: val } : p)),
    );
  };

  const createBatch = async (e?: FormEvent) => {
    if (e) e.preventDefault();
    if (!batchNumber.trim()) return;
    setSaving(true);
    setMessage(undefined);
    try {
      const { si, ti, mg, sr, remainingParams } = extractCoreChemicals();
      const created = await batchApi.create({
        batchNumber: batchNumber.trim(),
        furnaceId: selectedId,
        indicator,
        colorCode,
        si,
        ti,
        mg,
        sr,
        instruction,
        remarks: buildRemarksPayload(remarks, remainingParams, weightText, calculationDetails, actualValues),
        actualOperatorName: localStorage.getItem('actualOperatorName') || undefined,
      });
      setBatch(created);
      setBatchNumber('');
      setCustomParams([
        { id: 'si', name: 'Si', value: '0', unit: 'g' },
        { id: 'ti', name: 'Ti', value: '0', unit: 'g' },
        { id: 'mg', name: 'Mg', value: '0', unit: 'g' },
        { id: 'sr', name: 'Sr', value: '0', unit: 'g' },
      ]);
      setCalculationDetails([]);
      setActualValues({});
      setRemarks('');
      setWeightText('');
      setMessage('Batch started successfully. Form reset for next batch.');
    } catch (err) {
      setMessage((err as Error).message);
    } finally {
      setSaving(false);
    }
  };

  const updateBatch = async (e?: FormEvent) => {
    if (e) e.preventDefault();
    if (!batch) return;
    setSaving(true);
    setMessage(undefined);
    try {
      const { si, ti, mg, sr, remainingParams } = extractCoreChemicals();
      const updated = await batchApi.update(batch.id, {
        si,
        ti,
        mg,
        sr,
        colorCode,
        indicator,
        instruction,
        remarks: buildRemarksPayload(remarks, remainingParams, weightText, calculationDetails, actualValues),
        actualOperatorName: localStorage.getItem('actualOperatorName') || undefined,
      });
      setBatch(updated);
      setMessage('Batch updated successfully.');
    } catch (err) {
      setMessage((err as Error).message);
    } finally {
      setSaving(false);
    }
  };

  const clearBatch = async () => {
    if (!confirmClear) {
      setConfirmClear(true);
      return;
    }
    setSaving(true);
    setMessage(undefined);
    try {
      await batchApi.clear(selectedId, true, localStorage.getItem('actualOperatorName') || undefined);
      setConfirmClear(false);
      setBatch(null);
      setBatchNumber('');
      setColorCode('Yellow-blue');
      setIndicator(1);
      setInstruction('');
      setRemarks('');
      setWeightText('');
      setCustomParams([
        { id: 'si', name: 'Si', value: '0', unit: 'g' },
        { id: 'ti', name: 'Ti', value: '0', unit: 'g' },
        { id: 'mg', name: 'Mg', value: '0', unit: 'g' },
        { id: 'sr', name: 'Sr', value: '0', unit: 'g' },
      ]);
      setCalculationDetails([]);
      setActualValues({});
      setFurnaces((prev) => prev.map((f) => (f.id === selectedId ? { ...f, operationalStatus: 'Empty', operationalRemarks: 'Furnace Cleared' } : f)));
      setMessage('Furnace cleared successfully.');
    } catch (err) {
      setMessage((err as Error).message);
    } finally {
      setSaving(false);
    }
  };

  const closeBatch = async () => {
    if (!batch) return;
    setSaving(true);
    setMessage(undefined);
    try {
      await batchApi.close(batch.id, true, localStorage.getItem('actualOperatorName') || undefined);
      setBatch(null);
      setBatchNumber('');
      setColorCode('Yellow-blue');
      setIndicator(1);
      setInstruction('');
      setRemarks('');
      setWeightText('');
      setCustomParams([
        { id: 'si', name: 'Si', value: '0', unit: 'g' },
        { id: 'ti', name: 'Ti', value: '0', unit: 'g' },
        { id: 'mg', name: 'Mg', value: '0', unit: 'g' },
        { id: 'sr', name: 'Sr', value: '0', unit: 'g' },
      ]);
      setCalculationDetails([]);
      setActualValues({});
      setFurnaces((prev) => prev.map((f) => (f.id === selectedId ? { ...f, operationalStatus: 'Empty', operationalRemarks: undefined } : f)));
      setMessage('Batch closed successfully.');
    } catch (err) {
      setMessage((err as Error).message);
    } finally {
      setSaving(false);
    }
  };

  const handleSetOperationalStatus = async (status: string) => {
    if (!selectedId) return;
    setUpdatingStatus(true);
    try {
      const updated = await furnaceApi.updateOperationalStatus(selectedId, status, statusRemarks.trim() ? statusRemarks.trim() : undefined);
      setFurnaces((prev) => prev.map((f) => (f.id === selectedId ? updated : f)));
      setMessage(`Furnace ${selectedFurnace?.number || selectedId} status set to "${status}".`);
      setStatusRemarks('');
    } catch (err) {
      setMessage(`Failed to update furnace status: ${(err as Error).message}`);
    } finally {
      setUpdatingStatus(false);
    }
  };



  const handlePdfParsed = async (data: ParsedSpectroResultDto, isManualUpload: boolean = false) => {
    let targetFurnaceId = selectedId;
    let didChangeFurnace = false;

    let matchedFurnace: FurnaceDto | undefined = undefined;

    // -----------------------------------------------------------------
    // 3RD LETTER CONCEPT & AUTO-ACTIVATION:
    // "if Furnace is in any status if Batch will came for that particular
    // furance they do Furnace in active mode and Display Batch info as per
    // the Furnace number as per the 3rd letter concept"
    // -----------------------------------------------------------------
    let resolvedFurnaceNumber = data.furnaceNumber;
    if (!resolvedFurnaceNumber && data.batchNumber) {
      const trimmed = data.batchNumber.trim();
      if (trimmed.length >= 3) {
        const charCode = trimmed.toUpperCase().charCodeAt(2);
        if (charCode >= 65 && charCode <= 90) { // 'A'=65, 'B'=66 ... 'Z'=90
          resolvedFurnaceNumber = (charCode - 65) + 1;
          data.furnaceNumber = resolvedFurnaceNumber;
        }
      }
      if (!resolvedFurnaceNumber) {
        const cleanChars = trimmed.replace(/[^A-Za-z0-9]/g, '');
        if (cleanChars.length >= 3) {
          const charCode = cleanChars.toUpperCase().charCodeAt(2);
          if (charCode >= 65 && charCode <= 90) {
            resolvedFurnaceNumber = (charCode - 65) + 1;
            data.furnaceNumber = resolvedFurnaceNumber;
          }
        }
      }
      if (!resolvedFurnaceNumber) {
        const numMatch = trimmed.match(/^(?:FN|Furnace|F)?\s*(\d+)$/i);
        if (numMatch) {
          const num = parseInt(numMatch[1], 10);
          if (num >= 1 && num <= 25) {
            resolvedFurnaceNumber = num;
            data.furnaceNumber = resolvedFurnaceNumber;
          }
        }
      }
    }

    if (resolvedFurnaceNumber) {
      matchedFurnace = furnaces.find(f => {
        const fn = f.number.replace(/\D/g, '');
        return fn === resolvedFurnaceNumber?.toString();
      });

      if (!matchedFurnace) {
        try {
          const list = await furnaceApi.getAll();
          setFurnaces(list);
          matchedFurnace = list.find(f => f.number.replace(/\D/g, '') === resolvedFurnaceNumber?.toString());
        } catch (err) {
          console.error("Failed to refresh furnaces", err);
        }
      }

      if (matchedFurnace) {
        targetFurnaceId = matchedFurnace.id;
        if (matchedFurnace.id !== selectedId) {
          didChangeFurnace = true;
        }

        // Auto-activate furnace in active mode (Melting) regardless of previous status
        if (!matchedFurnace.isActive || matchedFurnace.operationalStatus !== 'Melting') {
          try {
            await furnaceApi.updateOperationalStatus(matchedFurnace.id, 'Melting');
            setFurnaces(prev => prev.map(f => f.id === matchedFurnace!.id ? { ...f, isActive: true, operationalStatus: 'Melting' } : f));
          } catch (err) {
            console.warn("Could not auto-activate furnace status", err);
          }
        }
      }
    } else {
      matchedFurnace = furnaces.find(f => f.id === selectedId);
      targetFurnaceId = selectedId;
      if (matchedFurnace) {
        data.furnaceNumber = parseInt(matchedFurnace.number.replace(/\D/g, '') || '1', 10);
      }
    }

    if (!matchedFurnace) {
      setPdfAlert({
        title: 'Auto-Start Blocked — Batch NOT Sent to Display',
        mistake: `PDF specifies Furnace ${data.furnaceNumber ?? 'Unknown'}, but this furnace is not registered in the system.`,
        solution: `1. Ensure the Furnace exists under Admin -> Furnaces.\n2. Or select an active Furnace from the left panel before uploading.`,
        rawWarnings: data.warnings,
        extracted: {
          furnaceNumber: data.furnaceNumber,
          batchNumber: data.batchNumber,
          partName: data.partName,
          partNo: data.partNo,
          gradeName: data.gradeName,
          furnaceWeight: data.furnaceWeight
        }
      });
      setMessage(`PDF parsed for Furnace ${data.furnaceNumber ?? 'Unknown'}, but that furnace is not found. Auto-apply aborted.`);
      return;
    }

    let currentBatch = batch;
    if (didChangeFurnace) {
      setSelectedId(targetFurnaceId);
      currentBatch = await batchApi.getActive(targetFurnaceId);
      setBatch(currentBatch);
    }

    const nextBatchNumber = data.batchNumber || batchNumber;
    let nextColorCode = colorCode;
    if (data.colorCode) {
      nextColorCode = data.colorCode;
    }

    let nextWeightText = weightText;
    if (data.furnaceWeight) {
      nextWeightText = `${data.furnaceWeight} KG`;
      setWeightText(nextWeightText);
    }

    let nextParams: CustomParamRow[] = [];
    let nextInstruction = 'Start Pouring';
    let hasAdditions = false;

    if (data.calculatedAdditions && Object.keys(data.calculatedAdditions).length > 0) {
      hasAdditions = Object.values(data.calculatedAdditions).some(v => Number(v) > 0);
      if (hasAdditions) {
        nextInstruction = 'Add these chemicals';
      }
    }

    // Comprehensive blocking validation: NEVER PASS ERRORS OR INCOMPLETE DATA TO DISPLAY!
    const rawWarnings = data.warnings || [];
    const hasMissingFields = !data.partName || !data.partNo || !data.gradeName || rawWarnings.some(w => w.includes('Missing Part Name'));
    const hasMappingError = rawWarnings.some(w => w.includes('Mapping Error') || w.includes('match failed') || w.includes('Mismatch'));
    const labChemicals = rawWarnings.filter(w => w.includes('Contact lab')).map(w => w.split(':')[0].trim());
    const hasLabRejection = labChemicals.length > 0;
    const isBatchNumberMissing = !nextBatchNumber || !nextBatchNumber.trim();
    const isFurnaceInvalid = !matchedFurnace;

    const isBlocked = hasMissingFields || hasMappingError || hasLabRejection || isBatchNumberMissing || isFurnaceInvalid || rawWarnings.length > 0;

    if (isBlocked) {
      let mistake = '';
      let solution = '';

      if (hasMissingFields) {
        const missingList = [
          !data.partName && 'Part Name',
          !data.partNo && 'Part Number',
          !data.gradeName && 'Grade Name'
        ].filter(Boolean).join(', ');
        mistake = `Missing Part Details: Could not extract ${missingList || 'mandatory part metadata'} from the uploaded Spectro PDF. Database validation cannot be performed.`;
        solution = `1. Check the Spectro PDF to ensure Sample Result Name follows 'SampleID/Grade/PartName/PartNo'.\n2. Verify the Part Name, Part Number, and Grade are registered in Admin Panel -> Master Specs.\n3. Or manually select the Furnace and enter the batch details in the form below.`;
      } else if (hasMappingError) {
        mistake = `Master Specification Mismatch (Mapping Error): The extracted recipe [Part: "${data.partName}", Part No: "${data.partNo}", Grade: "${data.gradeName}"] does not match any active specification in the Master Database.`;
        solution = `1. Open Admin Panel -> Master Specs.\n2. Add or update the specification so Part Name ("${data.partName}"), Part No ("${data.partNo}"), and Grade ("${data.gradeName}") match the Spectro report.\n3. Once updated in Master Specs, re-upload the PDF to auto-apply calculations, or fill the parameters manually below.`;
      } else if (hasLabRejection) {
        mistake = `Quality Control Rejection (Contact Lab): Measured values for [${labChemicals.join(', ')}] exceeded the Customer Maximum specification limit.`;
        solution = `1. Immediately contact the Quality / Metallurgy Lab before proceeding.\n2. Do NOT pour or release this batch without lab authorization.\n3. Additions cannot be automatically calculated for out-of-spec elements.`;
      } else if (isBatchNumberMissing) {
        mistake = `Missing Batch Code: Could not extract a valid Batch Number from the PDF.`;
        solution = `Please enter the Batch Number manually in the field below.`;
      } else if (isFurnaceInvalid) {
        mistake = `Furnace Code Mismatch: Furnace ${data.furnaceNumber ?? 'Unknown'} is not configured or not active in the system.`;
        solution = `1. Activate Furnace in Admin -> Furnaces, or select an active furnace above.`;
      } else {
        mistake = `Validation Warnings: The PDF was parsed with diagnostic warnings:\n${rawWarnings.join('\n')}`;
        solution = `Review the diagnostic warnings below and verify the Master Specs or enter parameters manually.`;
      }

      setPdfAlert({
        title: 'Auto-Start Blocked — Batch NOT Sent to Display',
        mistake,
        solution,
        rawWarnings,
        extracted: {
          furnaceNumber: data.furnaceNumber,
          batchNumber: nextBatchNumber,
          partName: data.partName,
          partNo: data.partNo,
          gradeName: data.gradeName,
          furnaceWeight: data.furnaceWeight
        }
      });

      // Populate form state safely for visual review, BUT DO NOT AUTO-START OR SAVE TO DISPLAY!
      setBatchNumber(nextBatchNumber);
      setColorCode(nextColorCode);
      setCustomParams([]);
      setCalculationDetails([]);
      setActualValues(data.actualValues || {});
      setInstruction(hasLabRejection ? 'Contact Lab Batch Rejected' : 'Auto-Start Blocked (Review Errors)');
      setMessage(`PDF Parse Error: Auto-start BLOCKED. Batch was NOT passed to Display.\n${mistake}`);
      return; // CRITICAL: RETURN EARLY! DO NOT CALL batchApi.create or batchApi.update!
    }

    // If completely valid with no warnings or blocking issues:
    setPdfAlert(null); // Clear any previous alert

    const coreAdditions: Record<string, string> = { si: '0', ti: '0', mg: '0', sr: '0' };
    const customExtra: CustomParamRow[] = [];
    if (data.calculatedAdditions && Object.keys(data.calculatedAdditions).length > 0) {
      Object.entries(data.calculatedAdditions).forEach(([k, v]) => {
        const lower = k.toLowerCase();
        if (lower === 'si' || lower === 'silicon') coreAdditions.si = String(v);
        else if (lower === 'ti' || lower === 'titanium' || lower === 'altib') coreAdditions.ti = String(v);
        else if (lower === 'mg' || lower === 'magnesium') coreAdditions.mg = String(v);
        else if (lower === 'sr' || lower === 'strontium' || lower === 'alsr') coreAdditions.sr = String(v);
        else customExtra.push({ id: k, name: k, value: String(v), unit: 'g' });
      });
    }
    nextParams = [
      { id: 'si', name: 'Si', value: coreAdditions.si, unit: 'g' },
      { id: 'ti', name: 'Ti', value: coreAdditions.ti, unit: 'g' },
      { id: 'mg', name: 'Mg', value: coreAdditions.mg, unit: 'g' },
      { id: 'sr', name: 'Sr', value: coreAdditions.sr, unit: 'g' },
      ...customExtra,
    ];

    const nextCalcDetails = data.calculationDetails || [];
    const nextActuals = data.actualValues || {};

    // Immediately update local dynamic states so target values, actual values, and breakdown render instantly
    setCalculationDetails(nextCalcDetails);
    setActualValues(nextActuals);
    setCustomParams(nextParams);
    setColorCode(nextColorCode);
    setInstruction(nextInstruction);
    if (nextWeightText) setWeightText(nextWeightText);
    if (nextBatchNumber) setBatchNumber(nextBatchNumber);

    setSaving(true);
    try {
      const { si, ti, mg, sr, remainingParams } = extractCoreChemicals(nextParams);
      const payloadRemarks = buildRemarksPayload(remarks, remainingParams, nextWeightText, nextCalcDetails, nextActuals);

      let msg = `PDF parsed successfully. Auto-filled available fields.`;

      if (currentBatch) {
        if (nextBatchNumber.trim() && currentBatch.batchNumber && currentBatch.batchNumber !== nextBatchNumber.trim()) {
          try {
            await batchApi.close(currentBatch.id, true);
          } catch (err) {
            console.warn("Batch was already closed or could not be closed:", err);
          }

          const created = await batchApi.create({
            batchNumber: nextBatchNumber.trim(),
            furnaceId: targetFurnaceId,
            indicator: 1, // Reset to Green
            colorCode: nextColorCode,
            si, ti, mg, sr,
            instruction: nextInstruction,
            remarks: payloadRemarks,
          });
          setBatch(created);
          setBatchNumber('');

          const meta = parseRemarksMetadata(created.remarks);
          setRemarks(meta?.userRemarks ?? created.remarks ?? '');
          setWeightText(meta?.weightText || nextWeightText);
          setCalculationDetails(meta?.calculationDetails && meta.calculationDetails.length > 0 ? meta.calculationDetails : nextCalcDetails);
          setActualValues(meta?.actualValues && Object.keys(meta.actualValues).length > 0 ? meta.actualValues : nextActuals);
          const loadedParams: CustomParamRow[] = [
            { id: 'si', name: 'Si', value: String(created.si ?? 0), unit: 'g' },
            { id: 'ti', name: 'Ti', value: String(created.ti ?? 0), unit: 'g' },
            { id: 'mg', name: 'Mg', value: String(created.mg ?? 0), unit: 'g' },
            { id: 'sr', name: 'Sr', value: String(created.sr ?? 0), unit: 'g' },
          ];
          if (meta?.params) {
            meta.params.forEach(p => {
              const lower = p.name.toLowerCase();
              if (!['si', 'ti', 'mg', 'sr'].includes(lower)) loadedParams.push(p);
            });
          }
          setCustomParams(loadedParams);

          setInstruction(created.instruction ?? '');
          setColorCode(created.colorCode || 'Yellow-blue');
          setIndicator(created.indicator);
          setMessage(`${msg}\nPrevious batch closed. Auto-started new batch seamlessly.`);
        } else {
          const updated = await batchApi.update(currentBatch.id, {
            batchNumber: nextBatchNumber,
            si, ti, mg, sr,
            colorCode: nextColorCode,
            indicator: 1,
            instruction: nextInstruction,
            remarks: payloadRemarks,
          });
          setBatch(updated);

          setColorCode(updated.colorCode || 'Yellow-blue');
          setIndicator(updated.indicator);
          setInstruction(updated.instruction ?? '');
          const meta = parseRemarksMetadata(updated.remarks);
          setRemarks(meta?.userRemarks ?? updated.remarks ?? '');
          setWeightText(meta?.weightText || nextWeightText);
          setCalculationDetails(meta?.calculationDetails && meta.calculationDetails.length > 0 ? meta.calculationDetails : nextCalcDetails);
          setActualValues(meta?.actualValues && Object.keys(meta.actualValues).length > 0 ? meta.actualValues : nextActuals);
          const loadedParams: CustomParamRow[] = [
            { id: 'si', name: 'Si', value: String(updated.si ?? 0), unit: 'g' },
            { id: 'ti', name: 'Ti', value: String(updated.ti ?? 0), unit: 'g' },
            { id: 'mg', name: 'Mg', value: String(updated.mg ?? 0), unit: 'g' },
            { id: 'sr', name: 'Sr', value: String(updated.sr ?? 0), unit: 'g' },
          ];
          if (meta?.params) {
            meta.params.forEach(p => {
              const lower = p.name.toLowerCase();
              if (!['si', 'ti', 'mg', 'sr'].includes(lower)) loadedParams.push(p);
            });
          }
          setCustomParams(loadedParams);

          setMessage(`${msg}\nAuto-updated active batch with fresh PDF data.`);
        }
      } else {
        const created = await batchApi.create({
          batchNumber: nextBatchNumber.trim(),
          furnaceId: targetFurnaceId,
          indicator: 1,
          colorCode: nextColorCode,
          si, ti, mg, sr,
          instruction: nextInstruction,
          remarks: payloadRemarks,
        });
        setBatch(created);
        setBatchNumber('');

        const meta = parseRemarksMetadata(created.remarks);
        setRemarks(meta?.userRemarks ?? created.remarks ?? '');
        setWeightText(meta?.weightText || nextWeightText);
        setCalculationDetails(meta?.calculationDetails && meta.calculationDetails.length > 0 ? meta.calculationDetails : nextCalcDetails);
        setActualValues(meta?.actualValues && Object.keys(meta.actualValues).length > 0 ? meta.actualValues : nextActuals);
        const loadedParams: CustomParamRow[] = [
          { id: 'si', name: 'Si', value: String(created.si ?? 0), unit: 'g' },
          { id: 'ti', name: 'Ti', value: String(created.ti ?? 0), unit: 'g' },
          { id: 'mg', name: 'Mg', value: String(created.mg ?? 0), unit: 'g' },
          { id: 'sr', name: 'Sr', value: String(created.sr ?? 0), unit: 'g' },
        ];
        if (meta?.params) {
          meta.params.forEach(p => {
            const lower = p.name.toLowerCase();
            if (!['si', 'ti', 'mg', 'sr'].includes(lower)) loadedParams.push(p);
          });
        }
        setCustomParams(loadedParams);

        setInstruction(created.instruction ?? '');
        setColorCode(created.colorCode || 'Yellow-blue');
        setIndicator(created.indicator);
        setMessage(`${msg}\nAuto-started new batch successfully.`);
      }
    } catch (err) {
      setMessage(`Error during auto-submit: ${(err as Error).message}`);
    } finally {
      setSaving(false);
    }
  };

  useEffect(() => {
    const unsubscribe = realtimeClient.onPdfParsed((data) => {
      void handlePdfParsed(data, false);
    });
    return () => unsubscribe();
  }, [furnaces, selectedId, batch, batchNumber, colorCode, weightText, remarks, customParams, calculationDetails]);

  useEffect(() => {
    const unSubStatus = realtimeClient.onFurnaceStatusChanged((statusData) => {
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
    return () => unSubStatus();
  }, []);

  const selectedFurnace = furnaces.find((f) => f.id === selectedId);

  // Compute total chemical additions in grams
  const totalAdditionsGrams = (parseFloat(getParamValue('si')) || 0) +
    (parseFloat(getParamValue('ti')) || 0) +
    (parseFloat(getParamValue('mg')) || 0) +
    (parseFloat(getParamValue('sr')) || 0) +
    customParams.filter(p => !['si', 'ti', 'mg', 'sr'].includes(p.name.toLowerCase()))
      .reduce((acc, p) => acc + (parseFloat(p.value) || 0), 0);

  const isRedTolerance = calculationDetails?.some(c => c.formula?.includes('Batch Rejected') || (c.maxSpec > 0 && c.actualValue > c.maxSpec && c.formula !== 'In Specification'));

  return (
    <div className="container-fluid p-0 d-flex flex-column h-100" style={{ overflow: 'hidden' }}>
      {/* Header Bar */}
      <header className="mb-2 flex-shrink-0 d-flex align-items-center justify-content-between" style={{ minHeight: '36px' }}>
        <div className="d-flex align-items-center gap-3">
          <h4 className="text-fm fw-bold mb-0" style={{ fontSize: '1rem', letterSpacing: '-0.2px' }}>Operator Panel</h4>
          <span className="text-fm-muted" style={{ fontSize: '0.72rem' }}>|
            <span className="ms-2 fw-semibold text-fm">{user?.displayName || 'System Administrator'}</span>
          </span>
        </div>
        {message && (
          <div className="alert alert-info py-1 px-3 mb-0 d-flex align-items-center gap-2 small shadow-sm" role="alert" style={{ fontSize: '0.78rem' }}>
            <i className="bi bi-info-circle-fill text-info"></i>
            <span>{message}</span>
            <button type="button" className="btn-close py-0" style={{ fontSize: '0.6rem' }} onClick={() => setMessage(undefined)}></button>
          </div>
        )}
      </header>

      <div className="internal-scroll pb-1 flex-grow-1" style={{ overflowX: 'hidden' }}>
        <div className="row g-2 m-0 h-100">
          {/* Left Column: Furnace Selection & Operational Status */}
          <div className="col-lg-4 p-1">
            <div className="op-card h-100 d-flex flex-column">
              <div className="d-flex align-items-center justify-content-between mb-1.5">
                <div className="op-label text-fm mb-0 fw-bold">
                  <i className="bi bi-fire text-danger"></i>
                  Select Furnace
                </div>
                {selectedFurnace?.operationalStatus && (
                  <span className="badge px-2 py-0.5 fw-bold small d-inline-flex align-items-center" style={{
                    backgroundColor:
                      (selectedFurnace?.operationalStatus === 'Empty' ? '#06b6d4' :
                        selectedFurnace?.operationalStatus === 'Melting' ? '#f97316' :
                          selectedFurnace?.operationalStatus === 'Maintenance' ? '#8b5cf6' :
                            selectedFurnace?.operationalStatus === 'Breakdown' ? '#eab308' :
                              selectedFurnace?.operationalStatus === 'Emergency' ? '#dc2626' : '#64748b'),
                    color: (selectedFurnace?.operationalStatus === 'Breakdown' ? '#18181b' : '#ffffff'),
                    fontSize: '0.75rem'
                  }}>
                    {selectedFurnace?.operationalStatus === 'Empty' ? (
                      <><i className="bi bi-circle me-1"></i> Empty</>
                    ) : selectedFurnace?.operationalStatus === 'Melting' ? (
                      <><i className="bi bi-fire me-1"></i> In Melting</>
                    ) : selectedFurnace?.operationalStatus === 'Maintenance' ? (
                      <><i className="bi bi-tools me-1"></i> Maintenance</>
                    ) : selectedFurnace?.operationalStatus === 'Breakdown' ? (
                      <><i className="bi bi-exclamation-triangle-fill me-1"></i> Breakdown</>
                    ) : selectedFurnace?.operationalStatus === 'Emergency' ? (
                      <><i className="bi bi-sign-stop-fill me-1"></i> Emergency</>
                    ) : (
                      <><i className="bi bi-clock-history me-1"></i> Waiting Batch</>
                    )}
                  </span>
                )}
              </div>
              <select
                className="form-select form-select-sm op-select mb-2"
                value={selectedId}
                onChange={(e) => {
                  const newId = Number(e.target.value);
                  setSelectedId(newId);
                  void loadBatch(newId);
                }}
              >
                {furnaces.map((f) => (
                  <option key={f.id} value={f.id}>
                    {f.number} — {f.name}
                  </option>
                ))}
              </select>

              {/* Current Batch Status - Full Detail */}
              <div className="mb-2">
                {loadingBatch ? (
                  <div className="p-2 rounded bg-fm-alt border border-fm border-opacity-15 d-flex align-items-center gap-2 text-fm-muted" style={{ fontSize: '0.8rem' }}>
                    <div className="spinner-border spinner-border-sm" />
                    <span>Loading batch data...</span>
                  </div>
                ) : batch ? (
                  <div className="p-2 rounded bg-fm-alt border border-fm border-opacity-15">
                    <div className="d-flex align-items-center justify-content-between mb-1">
                      <div className="fw-bold text-fm" style={{ fontSize: '0.92rem' }}>
                        {batch.batchNumber}
                      </div>
                      <span
                        className={`badge ${batch.indicator === 1
                          ? 'bg-success'
                          : batch.indicator === 2
                            ? 'bg-warning text-dark'
                            : 'bg-danger'
                          }`}
                        style={{ fontSize: '0.68rem' }}
                      >
                        {['GREEN', 'YELLOW', 'RED'][batch.indicator - 1]}
                      </span>
                    </div>

                    <div className="d-flex align-items-center justify-content-between mb-1" style={{ fontSize: '0.74rem' }}>
                      <span className="badge bg-primary bg-opacity-20 text-info border border-info border-opacity-25 py-0.5 px-1.5">
                        {batch.colorCode || 'Yellow-blue'}
                      </span>
                      {weightText && (
                        <span className="text-fm-muted fw-semibold">
                          Weight: <strong className="text-fm">{weightText}</strong>
                        </span>
                      )}
                    </div>

                    {/* Active Additions Breakdown */}
                    <div className="pt-1 border-top border-fm border-opacity-10 mb-1" style={{ fontSize: '0.72rem' }}>
                      {(() => {
                        if (isRedTolerance) {
                          return (
                            <span className="text-danger fw-bold d-inline-flex align-items-center">
                              <i className="bi bi-sign-stop-fill me-1"></i> Contact to Lab
                            </span>
                          );
                        }
                        const activeItems: string[] = [];
                        if (batch.si && batch.si > 0) activeItems.push(`Si: ${batch.si}g`);
                        if (batch.ti && batch.ti > 0) activeItems.push(`Ti: ${batch.ti}g`);
                        if (batch.mg && batch.mg > 0) activeItems.push(`Mg: ${batch.mg}g`);
                        if (batch.sr && batch.sr > 0) activeItems.push(`Sr: ${batch.sr}g`);
                        const meta = parseRemarksMetadata(batch.remarks);
                        if (meta?.params) {
                          meta.params.forEach(p => {
                            if (Number(p.value) > 0) activeItems.push(`${p.name}: ${p.value}${p.unit}`);
                          });
                        }
                        if (activeItems.length > 0) {
                          return (
                            <div className="d-flex flex-wrap gap-1 text-fm">
                              {activeItems.map((item, idx) => (
                                <span
                                  key={idx}
                                  className="badge bg-dark border border-secondary border-opacity-40 px-2 py-0.5"
                                  style={{ color: '#ffffff', backgroundColor: '#1e293b' }}
                                >
                                  <span style={{ color: '#ffffff', fontWeight: 600 }}>{item}</span>
                                </span>
                              ))}
                            </div>
                          );
                        }
                        return (
                          <span className="text-success fw-bold d-inline-flex align-items-center">
                            <i className="bi bi-check-circle-fill me-1"></i> Within Spec (0g additions)
                          </span>
                        );
                      })()}
                    </div>

                    {batch.instruction && (
                      <div className="text-truncate pt-1 border-top border-fm border-opacity-10" style={{ fontSize: '0.72rem' }} title={batch.instruction}>
                        <span className="text-fm-muted me-1">Inst:</span>
                        <strong className="text-warning">{batch.instruction}</strong>
                      </div>
                    )}
                  </div>
                ) : (
                  <div className="py-1.5 px-2 rounded bg-fm-alt border border-fm border-opacity-10 text-center text-fm-muted" style={{ fontSize: '0.78rem' }}>
                    No active batch on <strong className="text-fm">{selectedFurnace?.number || 'this furnace'}</strong>
                  </div>
                )}
              </div>

              {/* Furnace Operational Status Widget */}
              <div className="pt-2 border-top border-fm border-opacity-15 d-flex flex-column flex-grow-1">
                <div className="d-flex align-items-center justify-content-between mb-1.5">
                  <span className="op-label mb-0">
                    <i className="bi bi-activity text-primary"></i>
                    Operational Status
                  </span>
                  <span className="badge bg-secondary bg-opacity-20 text-fm border border-fm border-opacity-20" style={{ fontSize: '0.65rem' }}>
                    Live Shop Floor
                  </span>
                </div>

                {/* Current State Indicator Row */}
                <div className="p-1.5 mb-2 rounded bg-fm-alt border border-fm border-opacity-15 d-flex align-items-center justify-content-between">
                  <span className="text-fm-muted" style={{ fontSize: '0.72rem' }}>Current State:</span>
                  <span className="badge px-2 py-0.5 fw-bold d-inline-flex align-items-center" style={{
                    backgroundColor:
                      (selectedFurnace?.operationalStatus === 'Empty' ? '#06b6d4' :
                        selectedFurnace?.operationalStatus === 'Melting' ? '#f97316' :
                          selectedFurnace?.operationalStatus === 'Maintenance' ? '#8b5cf6' :
                            selectedFurnace?.operationalStatus === 'Breakdown' ? '#eab308' :
                              selectedFurnace?.operationalStatus === 'Emergency' ? '#dc2626' : '#64748b'),
                    color: (selectedFurnace?.operationalStatus === 'Breakdown' ? '#18181b' : '#ffffff'),
                    fontSize: '0.74rem'
                  }}>
                    {selectedFurnace?.operationalStatus === 'Empty' ? (
                      <><i className="bi bi-circle me-1"></i> Empty</>
                    ) : selectedFurnace?.operationalStatus === 'Melting' ? (
                      <><i className="bi bi-fire me-1"></i> In Melting</>
                    ) : selectedFurnace?.operationalStatus === 'Maintenance' ? (
                      <><i className="bi bi-tools me-1"></i> Maintenance</>
                    ) : selectedFurnace?.operationalStatus === 'Breakdown' ? (
                      <><i className="bi bi-exclamation-triangle-fill me-1"></i> Breakdown</>
                    ) : selectedFurnace?.operationalStatus === 'Emergency' ? (
                      <><i className="bi bi-sign-stop-fill me-1"></i> Emergency</>
                    ) : (
                      <><i className="bi bi-clock-history me-1"></i> Waiting Batch</>
                    )}
                  </span>
                </div>

                {/* Status Remarks Optional Input */}
                <div className="mb-2">
                  <input
                    type="text"
                    className="form-control form-control-sm op-select"
                    placeholder="Status remark (e.g. Relining patch, Heating 750C)..."
                    value={statusRemarks}
                    onChange={(e) => setStatusRemarks(e.target.value)}
                    style={{ fontSize: '0.78rem', padding: '0.35rem 0.6rem' }}
                  />
                </div>

                {/* 6 One-Touch Tactile Status Buttons */}
                <div className="status-btn-grid mb-1">
                  <button
                    type="button"
                    className={`status-touch-btn btn-empty ${selectedFurnace?.operationalStatus === 'Empty' ? 'active' : ''}`}
                    disabled={updatingStatus}
                    onClick={() => void handleSetOperationalStatus('Empty')}
                  >
                    <i className="bi bi-circle"></i>
                    <span>Empty</span>
                  </button>

                  <button
                    type="button"
                    className={`status-touch-btn btn-melting ${selectedFurnace?.operationalStatus === 'Melting' ? 'active' : ''}`}
                    disabled={updatingStatus}
                    onClick={() => void handleSetOperationalStatus('Melting')}
                  >
                    <i className="bi bi-fire"></i>
                    <span>In Melting</span>
                  </button>

                  <button
                    type="button"
                    className={`status-touch-btn btn-maintenance ${selectedFurnace?.operationalStatus === 'Maintenance' ? 'active' : ''}`}
                    disabled={updatingStatus}
                    onClick={() => void handleSetOperationalStatus('Maintenance')}
                  >
                    <i className="bi bi-tools"></i>
                    <span>Maintenance</span>
                  </button>

                  <button
                    type="button"
                    className={`status-touch-btn btn-breakdown ${selectedFurnace?.operationalStatus === 'Breakdown' ? 'active' : ''}`}
                    disabled={updatingStatus}
                    onClick={() => void handleSetOperationalStatus('Breakdown')}
                  >
                    <i className="bi bi-exclamation-triangle-fill"></i>
                    <span>Breakdown</span>
                  </button>

                  <button
                    type="button"
                    className={`status-touch-btn btn-emergency ${selectedFurnace?.operationalStatus === 'Emergency' ? 'active' : ''}`}
                    disabled={updatingStatus}
                    onClick={() => void handleSetOperationalStatus('Emergency')}
                  >
                    <i className="bi bi-sign-stop-fill"></i>
                    <span>Emergency</span>
                  </button>

                  <button
                    type="button"
                    className={`status-touch-btn btn-waiting ${selectedFurnace?.operationalStatus === 'WaitingForBatch' || !selectedFurnace?.operationalStatus ? 'active' : ''}`}
                    disabled={updatingStatus}
                    onClick={() => void handleSetOperationalStatus('WaitingForBatch')}
                  >
                    <i className="bi bi-clock-history"></i>
                    <span>Waiting Batch</span>
                  </button>
                </div>
              </div>
            </div>
          </div>

          {/* Right Column: Update Batch & Parameters */}
          <div className="col-lg-8 p-1">
            <div className="op-card h-100 d-flex flex-column justify-content-between">
              <div>
                {/* Header / Title */}
                <div className="d-flex align-items-center justify-content-between mb-2 pb-2 border-bottom border-fm border-opacity-15">
                  <div className="d-flex align-items-center gap-2">
                    <span className="furnace-header-pill">
                      F-{selectedFurnace?.number?.replace(/^F/i, '') || '1'}
                    </span>
                    <h5 className="op-card-title mb-0">
                      {batch
                        ? `Batch — ${batch.batchNumber}`
                        : `Start New Batch`}
                    </h5>
                  </div>
                  <div className="d-flex align-items-center gap-1.5" style={{ fontSize: '0.72rem', color: 'var(--fm-muted)' }}>
                    <span className="status-indicator-dot"></span>
                    <span style={{ fontWeight: 600 }}>Spectro Live</span>
                  </div>
                </div>

                <PdfUploader onParseSuccess={(data) => void handlePdfParsed(data, true)} />

                {/* Prominent High-Visibility Alert for PDF Mistakes & Solutions */}
                {pdfAlert && (
                  <div className="mb-2 p-2.5 rounded-2 border border-danger shadow-sm" style={{ backgroundColor: 'rgba(220, 38, 38, 0.08)' }}>
                    <div className="d-flex align-items-center justify-content-between mb-2 border-bottom border-danger border-opacity-25 pb-1">
                      <div className="d-flex align-items-center gap-2">
                        <i className="bi bi-x-octagon-fill text-danger fs-5"></i>
                        <div>
                          <h6 className="text-danger fw-bold mb-0">{pdfAlert.title}</h6>
                          <small className="text-secondary" style={{ fontSize: '0.72rem' }}>The factory Display screen has NOT been updated with this batch.</small>
                        </div>
                      </div>
                      <button
                        type="button"
                        className="btn btn-sm btn-outline-secondary py-0 px-2"
                        style={{ fontSize: '0.75rem' }}
                        onClick={() => setPdfAlert(null)}
                      >
                        Dismiss
                      </button>
                    </div>

                    {/* The Mistake Box */}
                    <div className="p-2 mb-1.5 rounded-2 border border-danger border-opacity-50" style={{ backgroundColor: 'rgba(239, 68, 68, 0.15)' }}>
                      <div className="text-danger fw-bold mb-0.5 small d-flex align-items-center gap-1">
                        <i className="bi bi-x-circle-fill"></i>
                        <span>THE MISTAKE:</span>
                      </div>
                      <div className="text-white small ps-3" style={{ whiteSpace: 'pre-line', fontSize: '0.78rem' }}>
                        {pdfAlert.mistake}
                      </div>
                    </div>

                    {/* The Correct Solution Box */}
                    <div className="p-2 mb-1.5 rounded-2 border border-success border-opacity-50" style={{ backgroundColor: 'rgba(16, 185, 129, 0.15)' }}>
                      <div className="text-success fw-bold mb-0.5 small d-flex align-items-center gap-1">
                        <i className="bi bi-lightbulb-fill"></i>
                        <span>ACTION REQUIRED:</span>
                      </div>
                      <div className="text-white small ps-3" style={{ whiteSpace: 'pre-line', fontSize: '0.78rem' }}>
                        {pdfAlert.solution}
                      </div>
                    </div>
                  </div>
                )}

                <form onSubmit={batch ? updateBatch : createBatch}>
                  {/* Form Inputs Row 1: Batch Number, Furnace Weight, Indicator, Color Code */}
                  <div className="row g-2 mb-2">
                    <div className="col-md-3">
                      <div className="op-label mb-1">
                        <i className="bi bi-upc-scan text-primary"></i>
                        Batch Number *
                      </div>
                      <input
                        className="form-control form-control-sm op-select fw-bold"
                        value={batch ? (batch.batchNumber || batchNumber) : batchNumber}
                        onChange={(e) => {
                          const val = e.target.value;
                          setBatchNumber(val);
                          if (batch) setBatch({ ...batch, batchNumber: val });
                        }}
                        placeholder="e.g. weu or B-2026-001"
                        required
                      />
                    </div>
                    <div className="col-md-3">
                      <div className="op-label mb-1">
                        <i className="bi bi-speedometer text-info"></i>
                        Furnace Weight (KG)
                      </div>
                      <input
                        className="form-control form-control-sm op-select"
                        value={weightText}
                        onChange={(e) => setWeightText(e.target.value)}
                        placeholder="e.g. 350 KG or 350"
                      />
                    </div>
                    <div className="col-md-3">
                      <div className="op-label mb-1">
                        <i className="bi bi-traffic-light text-warning"></i>
                        Indicator Status
                      </div>
                      <select
                        className="form-select form-select-sm op-select"
                        value={indicator}
                        onChange={(e) => setIndicator(Number(e.target.value) as IndicatorColor)}
                      >
                        <option value={1}>🟢 Green (Normal)</option>
                        <option value={2}>🟡 Yellow (Warning / Lab)</option>
                        <option value={3}>🔴 Red (Emergency / Stop)</option>
                      </select>
                    </div>
                    <div className="col-md-3">
                      <div className="op-label mb-1">
                        <i className="bi bi-palette text-info"></i>
                        Color Code
                      </div>
                      <select
                        className="form-select form-select-sm op-select"
                        value={colorCode}
                        onChange={(e) => setColorCode(e.target.value)}
                      >
                        {COLOR_CODE_OPTIONS.map((opt) => (
                          <option key={opt} value={opt}>
                            {opt}
                          </option>
                        ))}
                        {colorCode && !COLOR_CODE_OPTIONS.includes(colorCode as any) && (
                          <option value={colorCode}>
                            {colorCode}
                          </option>
                        )}
                      </select>
                    </div>
                  </div>

                  {/* Form Inputs Row 2: Instruction & Remarks */}
                  <div className="row g-2 mb-2">
                    <div className="col-md-6">
                      <div className="op-label mb-1">
                        <i className="bi bi-chat-left-dots text-warning"></i>
                        Instruction
                      </div>
                      <input
                        className="form-control form-control-sm op-select text-warning fw-semibold"
                        value={instruction}
                        onChange={(e) => setInstruction(e.target.value)}
                        placeholder="e.g. Start Pouring or Contact To lab"
                      />
                    </div>
                    <div className="col-md-6">
                      <div className="op-label mb-1">
                        <i className="bi bi-pencil-square text-secondary"></i>
                        Remarks
                      </div>
                      <input
                        className="form-control form-control-sm op-select"
                        value={remarks}
                        onChange={(e) => setRemarks(e.target.value)}
                        placeholder="Operator remarks..."
                      />
                    </div>
                  </div>

                  {/* Chemical Additions Section */}
                  <div className="mb-2">
                    <div className="d-flex align-items-center justify-content-between mb-1.5">
                      <div className="d-flex align-items-center gap-2">
                        <span className="op-label mb-0">
                          <i className="bi bi-calculator text-primary me-1"></i>
                          Chemical Additions (Grams)
                        </span>
                        <span className="text-fm-muted" style={{ fontSize: '0.68rem', fontStyle: 'italic' }}>
                          Auto-calculated or Manual
                        </span>
                      </div>
                      <button
                        type="button"
                        className="btn btn-sm btn-outline-primary py-0 px-2 d-flex align-items-center gap-1"
                        style={{ fontSize: '0.72rem', height: '26px' }}
                        onClick={addParameterRow}
                      >
                        <i className="bi bi-plus-circle"></i>
                        <span>Add Custom Chemical</span>
                      </button>
                    </div>

                    {/* Status Banners */}
                    {isRedTolerance && (
                      <div className="status-qa-banner status-qa-danger mb-2 shadow-sm">
                        <div className="d-flex align-items-center gap-2.5">
                          <div className="status-qa-icon text-danger">
                            <i className="bi bi-sign-stop-fill"></i>
                          </div>
                          <div className="status-qa-content">
                            <div className="status-qa-title">Contact to Lab — Spec Tolerance Exceeded</div>
                            <div className="status-qa-desc">Chemical readings exceed maximum tolerance. Hold melt and contact quality lab.</div>
                          </div>
                        </div>
                        <span className="badge bg-danger text-white px-2.5 py-1 fw-bold" style={{ fontSize: '0.72rem' }}>
                          <i className="bi bi-shield-x me-1"></i> ACTION REQUIRED
                        </span>
                      </div>
                    )}

                    {pdfAlert && (
                      <div className="status-qa-banner status-qa-blocked mb-2 shadow-sm">
                        <div className="d-flex align-items-center gap-2.5">
                          <div className="status-qa-icon text-warning">
                            <i className="bi bi-exclamation-triangle-fill"></i>
                          </div>
                          <div className="status-qa-content">
                            <div className="status-qa-title">Additions Blocked — Validation Error</div>
                            <div className="status-qa-desc">Cannot verify chemical additions against Master Specifications.</div>
                          </div>
                        </div>
                        <span className="badge bg-warning text-dark px-2.5 py-1 fw-bold" style={{ fontSize: '0.72rem' }}>
                          HOLD
                        </span>
                      </div>
                    )}

                    {!isRedTolerance && !pdfAlert && totalAdditionsGrams === 0 && (Object.keys(actualValues).length > 0 || batch) && (
                      <div className="status-qa-banner status-qa-good mb-2 shadow-sm">
                        <div className="d-flex align-items-center gap-2.5">
                          <div className="status-qa-icon text-success">
                            <i className="bi bi-check-circle-fill"></i>
                          </div>
                          <div className="status-qa-content">
                            <div className="status-qa-title">Chemistry Within Spec — Everything is Good</div>
                            <div className="status-qa-desc">All spectrometer elements meet standard master specifications. No additions needed.</div>
                          </div>
                        </div>
                        <span className="badge bg-success text-white px-2.5 py-1 fw-bold" style={{ fontSize: '0.72rem' }}>
                          <i className="bi bi-shield-check me-1"></i> QUALITY PASSED
                        </span>
                      </div>
                    )}

                    {/* Spectrometer Laboratory Element Analysis */}
                    {Object.keys(actualValues).length > 0 && (
                      <div className="p-1.5 px-2 mb-2 rounded bg-fm-alt border border-fm border-opacity-15">
                        <div className="d-flex align-items-center justify-content-between mb-1">
                          <span className="text-fm fw-bold" style={{ fontSize: '0.72rem' }}>
                            <i className="bi bi-speedometer2 text-info me-1"></i>
                            Spectrometer Laboratory Element Analysis (%):
                          </span>
                          <span className="badge bg-info bg-opacity-25 text-info border border-info border-opacity-25" style={{ fontSize: '0.65rem' }}>
                            Lab Readings
                          </span>
                        </div>
                        <div className="d-flex flex-wrap gap-1.5" style={{ fontSize: '0.74rem' }}>
                          {(() => {
                            // Only display chemicals mentioned in the Master Spec Database for this batch/part
                            const specChems = calculationDetails.length > 0
                              ? calculationDetails.map(c => c.chemical.toLowerCase())
                              : ['si', 'fe', 'cu', 'mn', 'mg', 'cr', 'ni', 'zn', 'ti', 'sr', 'pb', 'sn'];

                            const sortOrder = ['si', 'fe', 'cu', 'mn', 'mg', 'cr', 'ni', 'zn', 'ti', 'sr', 'pb', 'sn'];

                            const filtered = Object.entries(actualValues).filter(([el]) =>
                              specChems.includes(el.toLowerCase())
                            );

                            filtered.sort((a, b) => {
                              const ia = sortOrder.indexOf(a[0].toLowerCase());
                              const ib = sortOrder.indexOf(b[0].toLowerCase());
                              return (ia >= 0 ? ia : 99) - (ib >= 0 ? ib : 99);
                            });

                            return filtered.map(([el, val]) => (
                              <span
                                key={el}
                                className="badge bg-dark border border-secondary border-opacity-40 py-1 px-1.5 font-monospace"
                                style={{ color: '#ffffff', backgroundColor: '#1e293b' }}
                              >
                                <strong style={{ color: '#ffffff' }}>{el}:</strong>{' '}
                                <span style={{ color: '#ffffff', fontWeight: 600 }}>{typeof val === 'number' ? val.toFixed(3) : val}%</span>
                              </span>
                            ));
                          })()}
                        </div>
                      </div>
                    )}

                    {/* Chemical Addition Cards (Shows only chemicals requiring additions) */}
                    {(() => {
                      const hasAnyBatchData = Boolean(batch || batchNumber || Object.keys(actualValues).length > 0);

                      const siVal = parseFloat(getParamValue('si')) || 0;
                      const tiVal = parseFloat(getParamValue('ti')) || 0;
                      const mgVal = parseFloat(getParamValue('mg')) || 0;
                      const srVal = parseFloat(getParamValue('sr')) || 0;

                      // Only show cards for chemicals that require additions (> 0), or all if in manual blank mode
                      const showSi = !hasAnyBatchData || siVal > 0;
                      const showTi = !hasAnyBatchData || tiVal > 0;
                      const showMg = !hasAnyBatchData || mgVal > 0;
                      const showSr = !hasAnyBatchData || srVal > 0;

                      const visibleCoreCount = [showSi, showTi, showMg, showSr].filter(Boolean).length;
                      const colClass = visibleCoreCount === 1
                        ? 'col-12 col-md-6 mx-auto'
                        : visibleCoreCount === 2
                          ? 'col-6 col-md-6'
                          : visibleCoreCount === 3
                            ? 'col-6 col-md-4'
                            : 'col-6 col-md-3';

                      if (visibleCoreCount === 0 && customParams.filter(p => !['si', 'ti', 'mg', 'sr'].includes(p.name.toLowerCase())).length === 0) {
                        return (
                          <div className="p-3 mb-2 rounded bg-fm-alt border border-success border-opacity-35 text-center">
                            <div className="text-success fw-bold d-flex align-items-center justify-content-center gap-1.5 mb-1">
                              <i className="bi bi-check-circle-fill fs-5"></i>
                              <span>All Chemicals In Specification — No Additions Required</span>
                            </div>
                            <span className="text-fm-muted small">Status: Ready to Pour. All alloy concentrations meet target limits.</span>
                          </div>
                        );
                      }

                      return (
                        <div className="row g-2 mb-2">
                          {/* Silicon */}
                          {showSi && (
                            <div className={colClass}>
                              <div className="chemical-card h-100">
                                <div className="chemical-card-header">
                                  <span className="chemical-card-name text-fm">Silicon (Si)</span>
                                  <span className="badge bg-primary bg-opacity-20 text-info" style={{ fontSize: '0.65rem' }}>Si</span>
                                </div>
                                <div className="d-flex align-items-center justify-content-between text-fm-muted mb-1" style={{ fontSize: '0.68rem' }}>
                                  <span>Act: <strong className="text-fm">{actualValues['Si'] ?? actualValues['si'] ?? '—'}%</strong></span>
                                  <span>Tgt: <strong className="text-fm">{calculationDetails.find(c => c.chemical.toLowerCase().includes('si'))?.targetValue ?? '—'}%</strong></span>
                                </div>
                                <div className="input-group input-group-sm">
                                  <input
                                    type="number"
                                    step="any"
                                    min="0"
                                    className="form-control form-control-sm op-select fw-bold text-end"
                                    value={getParamValue('si')}
                                    onChange={(e) => setParamValue('si', e.target.value)}
                                    placeholder="0"
                                  />
                                  <span className="input-group-text bg-fm-alt text-fm-muted border-fm" style={{ fontSize: '0.72rem' }}>g</span>
                                </div>
                              </div>
                            </div>
                          )}

                          {/* Titanium */}
                          {showTi && (
                            <div className={colClass}>
                              <div className="chemical-card h-100">
                                <div className="chemical-card-header">
                                  <span className="chemical-card-name text-fm">Titanium (AlTiB)</span>
                                  <span className="badge bg-warning bg-opacity-20 text-warning" style={{ fontSize: '0.65rem' }}>Ti</span>
                                </div>
                                <div className="d-flex align-items-center justify-content-between text-fm-muted mb-1" style={{ fontSize: '0.68rem' }}>
                                  <span>Act: <strong className="text-fm">{actualValues['Ti'] ?? actualValues['ti'] ?? '—'}%</strong></span>
                                  <span>Tgt: <strong className="text-fm">{calculationDetails.find(c => c.chemical.toLowerCase().includes('ti'))?.targetValue ?? '—'}%</strong></span>
                                </div>
                                <div className="input-group input-group-sm">
                                  <input
                                    type="number"
                                    step="any"
                                    min="0"
                                    className="form-control form-control-sm op-select fw-bold text-end"
                                    value={getParamValue('ti')}
                                    onChange={(e) => setParamValue('ti', e.target.value)}
                                    placeholder="0"
                                  />
                                  <span className="input-group-text bg-fm-alt text-fm-muted border-fm" style={{ fontSize: '0.72rem' }}>g</span>
                                </div>
                              </div>
                            </div>
                          )}

                          {/* Magnesium */}
                          {showMg && (
                            <div className={colClass}>
                              <div className="chemical-card h-100">
                                <div className="chemical-card-header">
                                  <span className="chemical-card-name text-fm">Magnesium (Mg)</span>
                                  <span className="badge bg-success bg-opacity-20 text-success" style={{ fontSize: '0.65rem' }}>Mg</span>
                                </div>
                                <div className="d-flex align-items-center justify-content-between text-fm-muted mb-1" style={{ fontSize: '0.68rem' }}>
                                  <span>Act: <strong className="text-fm">{actualValues['Mg'] ?? actualValues['mg'] ?? '—'}%</strong></span>
                                  <span>Tgt: <strong className="text-fm">{calculationDetails.find(c => c.chemical.toLowerCase().includes('mg'))?.targetValue ?? '—'}%</strong></span>
                                </div>
                                <div className="input-group input-group-sm">
                                  <input
                                    type="number"
                                    step="any"
                                    min="0"
                                    className="form-control form-control-sm op-select fw-bold text-end"
                                    value={getParamValue('mg')}
                                    onChange={(e) => setParamValue('mg', e.target.value)}
                                    placeholder="0"
                                  />
                                  <span className="input-group-text bg-fm-alt text-fm-muted border-fm" style={{ fontSize: '0.72rem' }}>g</span>
                                </div>
                              </div>
                            </div>
                          )}

                          {/* Strontium */}
                          {showSr && (
                            <div className={colClass}>
                              <div className="chemical-card h-100">
                                <div className="chemical-card-header">
                                  <span className="chemical-card-name text-fm">Strontium (AlSr)</span>
                                  <span className="badge bg-info bg-opacity-20 text-info" style={{ fontSize: '0.65rem' }}>Sr</span>
                                </div>
                                <div className="d-flex align-items-center justify-content-between text-fm-muted mb-1" style={{ fontSize: '0.68rem' }}>
                                  <span>Act: <strong className="text-fm">{actualValues['Sr'] ?? actualValues['sr'] ?? '—'}%</strong></span>
                                  <span>Tgt: <strong className="text-fm">{calculationDetails.find(c => c.chemical.toLowerCase().includes('sr'))?.targetValue ?? '—'}%</strong></span>
                                </div>
                                <div className="input-group input-group-sm">
                                  <input
                                    type="number"
                                    step="any"
                                    min="0"
                                    className="form-control form-control-sm op-select fw-bold text-end"
                                    value={getParamValue('sr')}
                                    onChange={(e) => setParamValue('sr', e.target.value)}
                                    placeholder="0"
                                  />
                                  <span className="input-group-text bg-fm-alt text-fm-muted border-fm" style={{ fontSize: '0.72rem' }}>g</span>
                                </div>
                              </div>
                            </div>
                          )}
                        </div>
                      );
                    })()}

                    {/* Custom Chemical Parameter Rows */}
                    {customParams.filter(p => !['si', 'ti', 'mg', 'sr'].includes(p.name.toLowerCase())).map((param) => (
                      <div key={param.id} className="row g-2 align-items-center mb-1.5 p-1.5 rounded bg-fm-alt border border-fm border-opacity-15">
                        <div className="col-4">
                          <input
                            type="text"
                            className="form-control form-control-sm op-select"
                            value={param.name}
                            onChange={(e) => updateParameterRow(param.id, 'name', e.target.value)}
                            placeholder="Chemical name"
                          />
                        </div>
                        <div className="col-4">
                          <input
                            type="number"
                            step="any"
                            min="0"
                            className="form-control form-control-sm op-select fw-bold text-end"
                            value={param.value}
                            onChange={(e) => updateParameterRow(param.id, 'value', e.target.value)}
                            placeholder="Grams"
                          />
                        </div>
                        <div className="col-3">
                          <select
                            className="form-select form-select-sm op-select"
                            value={param.unit}
                            onChange={(e) => updateParameterRow(param.id, 'unit', e.target.value)}
                          >
                            {UNIT_OPTIONS.map((u) => (
                              <option key={u} value={u}>{u}</option>
                            ))}
                          </select>
                        </div>
                        <div className="col-1 text-end">
                          <button
                            type="button"
                            className="btn btn-sm btn-outline-danger p-0"
                            style={{ width: '26px', height: '26px', display: 'inline-flex', alignItems: 'center', justifyContent: 'center' }}
                            onClick={() => removeParameterRow(param.id)}
                            title="Remove"
                          >
                            <i className="bi bi-trash3"></i>
                          </button>
                        </div>
                      </div>
                    ))}

                    {/* Calculation Details Accordion */}
                    {calculationDetails.filter(calc => calc.additionGrams > 0 || calc.formula?.toLowerCase().includes('over')).length > 0 && (
                      <div className="mt-1">
                        <div className="accordion accordion-flush bg-transparent" id="calcAccordion">
                          <div className="accordion-item bg-transparent border-0">
                            <h2 className="accordion-header" id="calcHeading">
                              <button className="accordion-button collapsed bg-fm-alt text-info border border-fm border-opacity-25 rounded-2 py-1 px-2.5 shadow-none text-decoration-none" style={{ fontSize: '0.8rem' }} type="button" data-bs-toggle="collapse" data-bs-target="#calcCollapse" aria-expanded="false" aria-controls="calcCollapse">
                                <i className="bi bi-diagram-3 me-1.5"></i>
                                <span className="small fw-bold">View Chemical Calculation Breakdown</span>
                              </button>
                            </h2>
                            <div id="calcCollapse" className="accordion-collapse collapse" aria-labelledby="calcHeading" data-bs-parent="#calcAccordion">
                              <div className="accordion-body px-0 py-1" style={{ maxHeight: '110px', overflowY: 'auto' }}>
                                {calculationDetails.filter(calc => calc.additionGrams > 0 || calc.formula?.toLowerCase().includes('over')).map((calc, idx) => (
                                  <div key={idx} className="p-2 mb-1 bg-fm-dark bg-opacity-25 rounded-2 border border-fm border-opacity-25 text-fm-muted small">
                                    <div className="d-flex justify-content-between mb-1">
                                      <strong className="text-fm">{calc.chemical} Addition</strong>
                                      <span className="text-info fw-bold">{calc.additionGrams} g</span>
                                    </div>
                                    <div className="d-flex gap-3 mb-1" style={{ fontSize: '0.75rem' }}>
                                      <span>Actual: <strong className="text-fm">{calc.actualValue}</strong></span>
                                      <span>Min: <strong className="text-fm">{calc.minSpec}</strong></span>
                                      <span>Max: <strong className="text-fm">{calc.maxSpec}</strong></span>
                                    </div>
                                    <div className="bg-fm-alt p-1.5 rounded text-muted font-monospace" style={{ fontSize: '0.72rem' }}>
                                      <div>Formula: {calc.formula}</div>
                                    </div>
                                  </div>
                                ))}
                              </div>
                            </div>
                          </div>
                        </div>
                      </div>
                    )}
                  </div>

                  {/* Furnace Summary Strip */}
                  <div className="furnace-telemetry-panel mb-2">
                    <div className="d-flex align-items-center gap-0 text-fm" style={{ fontSize: '0.78rem' }}>
                      <div className="pe-3 me-3 border-end border-fm border-opacity-15">
                        <span className="text-fm-muted me-1" style={{ fontSize: '0.7rem' }}>FURNACE</span>
                        <span className="fw-bold">{selectedFurnace?.number} — {selectedFurnace?.name || 'Standard Unit'}</span>
                      </div>
                      <div className="pe-3 me-3 border-end border-fm border-opacity-15">
                        <span className="text-fm-muted me-1" style={{ fontSize: '0.7rem' }}>WEIGHT</span>
                        <span className="fw-bold text-info">{weightText ? `${weightText}` : '—'}</span>
                      </div>
                      <div className="pe-3 me-3 border-end border-fm border-opacity-15">
                        <span className="text-fm-muted me-1" style={{ fontSize: '0.7rem' }}>TOTAL ADDITIONS</span>
                        <span className="fw-bold text-warning">{totalAdditionsGrams} g</span>
                      </div>
                      <div>
                        <span className="text-fm-muted me-1" style={{ fontSize: '0.7rem' }}>SPEC ENGINE</span>
                        <span className="fw-bold text-success" style={{ fontSize: '0.78rem' }}>Active</span>
                      </div>
                    </div>
                  </div>

                  {/* Bottom Row: Action Buttons */}
                  <div className="d-flex gap-2 justify-content-end pt-1">
                    <button
                      type="submit"
                      className={batch ? "btn btn-sm btn-op-update flex-grow-1" : "btn btn-sm btn-op-start flex-grow-1"}
                      style={{ height: '36px', padding: '0.25rem 0.5rem' }}
                      disabled={saving || (!batch && !batchNumber.trim())}
                    >
                      {batch ? (
                        <><i className="bi bi-check2-circle"></i> Update Batch</>
                      ) : (
                        <><i className="bi bi-play-circle-fill"></i> Start Batch</>
                      )}
                    </button>
                    {batch && (
                      <>
                        <button
                          type="button"
                          className="btn btn-sm btn-op-close"
                          style={{ height: '36px', padding: '0.25rem 0.65rem' }}
                          onClick={() => void closeBatch()}
                          disabled={saving}
                        >
                          <i className="bi bi-archive-fill"></i> Close
                        </button>
                        <button
                          type="button"
                          className="btn btn-sm btn-op-clear"
                          style={{ height: '36px', padding: '0.25rem 0.65rem' }}
                          onClick={() => void clearBatch()}
                          disabled={saving}
                        >
                          <i className="bi bi-trash3-fill"></i> {confirmClear ? 'Confirm?' : 'Clear'}
                        </button>
                      </>
                    )}
                  </div>
                </form>
              </div>
            </div>
          </div>
        </div>
      </div>


      {/* Operator Name Prompt Modal */}
      {showNamePrompt && (
        <div className="modal d-block" style={{ backgroundColor: 'rgba(0,0,0,0.85)', zIndex: 1050 }}>
          <div className="modal-dialog modal-dialog-centered">
            <div className="modal-content bg-fm-alt text-fm border-fm">
              <div className="modal-header border-fm">
                <h5 className="modal-title text-info fw-bold">Operator Identification</h5>
              </div>
              <form onSubmit={saveOperatorName}>
                <div className="modal-body">
                  <p className="small text-fm-muted mb-3">
                    Please enter your actual name for audit history and display purposes.
                  </p>
                  <label className="form-label text-light">Your Name</label>
                  <input
                    type="text"
                    className="form-control bg-fm-alt text-fm border-fm"
                    value={operatorNameInput}
                    onChange={(e) => setOperatorNameInput(e.target.value)}
                    required
                    autoFocus
                    placeholder="e.g. John Doe"
                  />
                </div>
                <div className="modal-footer border-fm">
                  <button type="submit" className="btn btn-primary px-4" disabled={!operatorNameInput.trim()}>
                    Proceed
                  </button>
                </div>
              </form>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
