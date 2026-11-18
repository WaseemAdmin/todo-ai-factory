import { TestBed } from '@angular/core/testing';
import { TodoEditor } from './todo-editor';

describe('TodoEditor', () => {
  it('shows required title validation and blocks blank submission', async () => {
    await TestBed.configureTestingModule({ imports: [TodoEditor] }).compileComponents();
    const fixture = TestBed.createComponent(TodoEditor);
    fixture.detectChanges();
    const editor = fixture.componentInstance;
    const save = vi.spyOn(editor.save, 'emit');
    editor.form.controls.title.setValue('   ');
    editor.submit();
    fixture.detectChanges();
    expect(save).not.toHaveBeenCalled();
    expect(fixture.nativeElement.querySelector('.field-error')?.textContent).toContain('Enter a title');
    editor.form.controls.title.setValue('  valid  ');
    editor.submit();
    expect(save).toHaveBeenCalledWith(expect.objectContaining({ title: 'valid' }));
  });

  it('accepts a trimmed 200 character title and rejects 201 characters', async () => {
    await TestBed.configureTestingModule({ imports: [TodoEditor] }).compileComponents();
    const fixture = TestBed.createComponent(TodoEditor);
    const editor = fixture.componentInstance;
    const save = vi.spyOn(editor.save, 'emit');
    editor.form.controls.title.setValue('  ' + 'x'.repeat(200) + '  ');
    expect(editor.form.controls.title.valid).toBe(true);
    editor.submit();
    expect(save).toHaveBeenCalledWith(expect.objectContaining({ title: 'x'.repeat(200) }));
    save.mockClear();
    editor.form.controls.title.setValue(' ' + 'x'.repeat(201) + ' ');
    expect(editor.form.controls.title.invalid).toBe(true);
    editor.submit();
    expect(save).not.toHaveBeenCalled();
  });
});
